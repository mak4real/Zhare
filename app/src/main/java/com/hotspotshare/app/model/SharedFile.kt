package com.hotspotshare.app.model

import android.net.Uri
import java.io.File

data class SharedFile(
    val uri: Uri,
    val name: String,
    val size: Long,
    val mimeType: String = "*/*"
) {
    val formattedSize: String
        get() = formatFileSize(size)

    companion object {
        fun formatFileSize(bytes: Long): String {
            if (bytes <= 0) return "0 B"
            val units = arrayOf("B", "KB", "MB", "GB", "TB")
            val digitGroups = (Math.log10(bytes.toDouble()) / Math.log10(1024.0)).toInt()
            val finalGroup = digitGroups.coerceIn(0, units.size - 1)
            val value = bytes / Math.pow(1024.0, finalGroup.toDouble())
            return String.format("%.2f %s", value, units[finalGroup])
        }
    }
}

data class ReceivedFile(
    val file: File,
    val name: String,
    val size: Long,
    val timestamp: Long = System.currentTimeMillis()
) {
    val formattedSize: String
        get() = SharedFile.formatFileSize(size)
}
