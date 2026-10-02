package com.hotspotshare.app.server

import android.content.Context
import android.os.Build
import android.os.Environment
import com.hotspotshare.app.model.SharedFile
import fi.iki.elonen.NanoHTTPD
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.io.FileInputStream
import java.io.InputStream
import java.net.URLDecoder
import java.util.concurrent.CopyOnWriteArrayList

class HotspotHttpServer(
    private val context: Context,
    port: Int = 8888
) : NanoHTTPD(port) {

    val sharedFiles = CopyOnWriteArrayList<SharedFile>()
    var outboundText: String = ""
    var inboundText: String = ""
    var onFileReceived: ((File) -> Unit)? = null
    var onClipboardUpdated: ((String) -> Unit)? = null

    override fun serve(session: IHTTPSession): Response {
        val uri = session.uri
        val method = session.method

        try {
            return when {
                // Static Assets
                uri == "/" || uri == "/index.html" -> serveAsset("web/index.html", "text/html")
                uri == "/style.css" -> serveAsset("web/style.css", "text/css")
                uri == "/app.js" -> serveAsset("web/app.js", "application/javascript")

                // Device Info API
                uri == "/api/info" && method == Method.GET -> {
                    val info = JSONObject().apply {
                        put("device", "${Build.MANUFACTURER.replaceFirstChar { it.uppercase() }} ${Build.MODEL}")
                        put("filesCount", sharedFiles.size)
                    }
                    newFixedLengthResponse(Response.Status.OK, "application/json", info.toString())
                }

                // List Shared Files API
                uri == "/api/files" && method == Method.GET -> {
                    val jsonArray = JSONArray()
                    sharedFiles.forEachIndexed { index, file ->
                        val obj = JSONObject().apply {
                            put("id", index)
                            put("name", file.name)
                            put("size", file.size)
                            put("mimeType", file.mimeType)
                        }
                        jsonArray.put(obj)
                    }
                    newFixedLengthResponse(Response.Status.OK, "application/json", jsonArray.toString())
                }

                // Download File API: /api/download/{index}
                uri.startsWith("/api/download/") && method == Method.GET -> {
                    val idStr = uri.removePrefix("/api/download/")
                    val id = idStr.toIntOrNull()
                    if (id != null && id in 0 until sharedFiles.size) {
                        serveDownload(sharedFiles[id])
                    } else {
                        newFixedLengthResponse(Response.Status.NOT_FOUND, "text/plain", "File not found")
                    }
                }

                // Upload File API: /api/upload
                uri == "/api/upload" && method == Method.POST -> {
                    handleUpload(session)
                }

                // Clipboard API
                uri == "/api/clipboard" -> {
                    if (method == Method.GET) {
                        newFixedLengthResponse(Response.Status.OK, "text/plain", outboundText)
                    } else if (method == Method.POST) {
                        val map = HashMap<String, String>()
                        session.parseBody(map)
                        val postData = map["postData"] ?: ""
                        inboundText = postData
                        onClipboardUpdated?.invoke(postData)
                        newFixedLengthResponse(Response.Status.OK, "application/json", "{\"status\":\"ok\"}")
                    } else {
                        newFixedLengthResponse(Response.Status.METHOD_NOT_ALLOWED, "text/plain", "Method not allowed")
                    }
                }

                else -> newFixedLengthResponse(Response.Status.NOT_FOUND, "text/plain", "Not Found")
            }
        } catch (e: Exception) {
            e.printStackTrace()
            return newFixedLengthResponse(Response.Status.INTERNAL_ERROR, "text/plain", "Server Error: ${e.message}")
        }
    }

    private fun serveAsset(assetPath: String, mimeType: String): Response {
        return try {
            val stream: InputStream = context.assets.open(assetPath)
            val available = stream.available().toLong()
            newChunkedResponse(Response.Status.OK, mimeType, stream)
        } catch (e: Exception) {
            newFixedLengthResponse(Response.Status.NOT_FOUND, "text/plain", "Asset not found: $assetPath")
        }
    }

    private fun serveDownload(file: SharedFile): Response {
        return try {
            val inputStream = context.contentResolver.openInputStream(file.uri)
                ?: return newFixedLengthResponse(Response.Status.NOT_FOUND, "text/plain", "Cannot open file stream")

            val response = newChunkedResponse(
                Response.Status.OK,
                file.mimeType.ifEmpty { "application/octet-stream" },
                inputStream
            )
            // Encode filename for content disposition
            val encodedName = URLDecoder.decode(file.name, "UTF-8").replace("\"", "")
            response.addHeader("Content-Disposition", "attachment; filename=\"$encodedName\"")
            response.addHeader("Content-Length", file.size.toString())
            response.addHeader("Accept-Ranges", "bytes")
            response
        } catch (e: Exception) {
            e.printStackTrace()
            newFixedLengthResponse(Response.Status.INTERNAL_ERROR, "text/plain", "Download error: ${e.message}")
        }
    }

    private fun handleUpload(session: IHTTPSession): Response {
        val files = HashMap<String, String>()
        try {
            session.parseBody(files)

            // Target storage directory: Downloads/HotspotShare
            val downloadsDir = Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS)
            val targetDir = File(downloadsDir, "HotspotShare")
            if (!targetDir.exists()) {
                targetDir.mkdirs()
            }

            // Find uploaded files in parameter map
            val params = session.parameters
            var uploadedFileName: String? = null
            var savedFile: File? = null

            for ((key, tempPath) in files) {
                // Determine original filename from parameters or default
                val paramList = params[key]
                val originalName = paramList?.firstOrNull() ?: "upload_${System.currentTimeMillis()}"
                val safeName = File(originalName).name // Sanitize path traversal

                val tempFile = File(tempPath)
                if (tempFile.exists()) {
                    var dest = File(targetDir, safeName)
                    // If file already exists, avoid overwriting by appending counter
                    if (dest.exists()) {
                        val baseName = dest.nameWithoutExtension
                        val ext = dest.extension.let { if (it.isNotEmpty()) ".$it" else "" }
                        var counter = 1
                        while (dest.exists()) {
                            dest = File(targetDir, "${baseName}_$counter$ext")
                            counter++
                        }
                    }

                    // Move or copy temp file to final destination
                    if (!tempFile.renameTo(dest)) {
                        tempFile.copyTo(dest, overwrite = true)
                        tempFile.delete()
                    }

                    uploadedFileName = dest.name
                    savedFile = dest
                    onFileReceived?.invoke(dest)
                }
            }

            val resp = JSONObject().apply {
                put("success", true)
                put("filename", uploadedFileName ?: "file")
            }
            return newFixedLengthResponse(Response.Status.OK, "application/json", resp.toString())
        } catch (e: Exception) {
            e.printStackTrace()
            return newFixedLengthResponse(Response.Status.INTERNAL_ERROR, "application/json", "{\"error\":\"${e.message}\"}")
        }
    }
}
