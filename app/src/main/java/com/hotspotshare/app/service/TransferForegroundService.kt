package com.hotspotshare.app.service

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationCompat
import com.hotspotshare.app.MainActivity
import com.hotspotshare.app.R
import com.hotspotshare.app.model.ReceivedFile
import com.hotspotshare.app.model.SharedFile
import com.hotspotshare.app.network.NetworkUtils
import com.hotspotshare.app.server.HotspotHttpServer
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import java.io.File

class TransferForegroundService : Service() {

    companion object {
        const val ACTION_START = "com.hotspotshare.app.ACTION_START"
        const val ACTION_STOP = "com.hotspotshare.app.ACTION_STOP"
        const val CHANNEL_ID = "hotspot_share_service_channel"
        const val NOTIFICATION_ID = 1001

        private val _isRunning = MutableStateFlow(false)
        val isRunning: StateFlow<Boolean> = _isRunning.asStateFlow()

        private val _serverUrl = MutableStateFlow("")
        val serverUrl: StateFlow<String> = _serverUrl.asStateFlow()

        private val _sharedFiles = MutableStateFlow<List<SharedFile>>(emptyList())
        val sharedFiles: StateFlow<List<SharedFile>> = _sharedFiles.asStateFlow()

        private val _receivedFiles = MutableStateFlow<List<ReceivedFile>>(emptyList())
        val receivedFiles: StateFlow<List<ReceivedFile>> = _receivedFiles.asStateFlow()

        private val _sharedClipboard = MutableStateFlow("")
        val sharedClipboard: StateFlow<String> = _sharedClipboard.asStateFlow()

        private var serverInstance: HotspotHttpServer? = null

        fun addSharedFiles(newFiles: List<SharedFile>) {
            val current = _sharedFiles.value.toMutableList()
            current.addAll(newFiles)
            _sharedFiles.value = current
            serverInstance?.sharedFiles?.clear()
            serverInstance?.sharedFiles?.addAll(current)
        }

        fun removeSharedFile(file: SharedFile) {
            val current = _sharedFiles.value.toMutableList()
            current.remove(file)
            _sharedFiles.value = current
            serverInstance?.sharedFiles?.clear()
            serverInstance?.sharedFiles?.addAll(current)
        }

        fun clearSharedFiles() {
            _sharedFiles.value = emptyList()
            serverInstance?.sharedFiles?.clear()
        }

        fun setSharedClipboard(text: String, updateServer: Boolean = true) {
            _sharedClipboard.value = text
            if (updateServer) {
                serverInstance?.sharedClipboard = text
            }
        }

        fun startService(context: Context) {
            val intent = Intent(context, TransferForegroundService::class.java).apply {
                action = ACTION_START
            }
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                context.startForegroundService(intent)
            } else {
                context.startService(intent)
            }
        }

        fun stopService(context: Context) {
            val intent = Intent(context, TransferForegroundService::class.java).apply {
                action = ACTION_STOP
            }
            context.startService(intent)
        }
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        createNotificationChannel()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_START -> startServer()
            ACTION_STOP -> stopServer()
        }
        return START_NOT_STICKY
    }

    private fun startServer() {
        if (_isRunning.value) return

        val ip = NetworkUtils.getLocalIpAddress() ?: "127.0.0.1"
        val port = NetworkUtils.DEFAULT_PORT
        val url = "http://$ip:$port"

        try {
            val server = HotspotHttpServer(applicationContext, port)
            server.sharedFiles.addAll(_sharedFiles.value)
            server.onFileReceived = { file ->
                handleIncomingFile(file)
            }
            server.onClipboardUpdated = { text ->
                _sharedClipboard.value = text
            }
            server.sharedClipboard = _sharedClipboard.value
            server.start()
            serverInstance = server

            _serverUrl.value = url
            _isRunning.value = true

            // Start UDP Auto-Discovery beacon broadcaster and listener
            com.hotspotshare.app.network.DiscoveryManager.startDiscovery(applicationContext)

            val notification = buildNotification(url)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                startForeground(
                    NOTIFICATION_ID,
                    notification,
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
                        ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC
                    } else {
                        0
                    }
                )
            } else {
                startForeground(NOTIFICATION_ID, notification)
            }
        } catch (e: Exception) {
            e.printStackTrace()
            _isRunning.value = false
            stopSelf()
        }
    }

    private fun handleIncomingFile(file: File) {
        val received = ReceivedFile(
            file = file,
            name = file.name,
            size = file.length(),
            timestamp = System.currentTimeMillis()
        )
        val current = _receivedFiles.value.toMutableList()
        current.add(0, received)
        _receivedFiles.value = current
    }

    private fun stopServer() {
        try {
            com.hotspotshare.app.network.DiscoveryManager.stopDiscovery()
            serverInstance?.stop()
            serverInstance = null
        } catch (e: Exception) {
            e.printStackTrace()
        }
        _isRunning.value = false
        _serverUrl.value = ""
        stopForeground(STOP_FOREGROUND_REMOVE)
        stopSelf()
    }

    private fun buildNotification(url: String): Notification {
        val openIntent = Intent(this, MainActivity::class.java)
        val openPendingIntent = PendingIntent.getActivity(
            this, 0, openIntent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        val stopIntent = Intent(this, TransferForegroundService::class.java).apply {
            action = ACTION_STOP
        }
        val stopPendingIntent = PendingIntent.getService(
            this, 1, stopIntent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle("HotspotShare is Active")
            .setContentText("Open browser at $url")
            .setSmallIcon(R.drawable.ic_launcher_foreground)
            .setContentIntent(openPendingIntent)
            .addAction(0, "Stop Server", stopPendingIntent)
            .setOngoing(true)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .build()
    }

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(
                CHANNEL_ID,
                "HotspotShare Transfer Service",
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = "Shows active local transfer server notifications"
            }
            val manager = getSystemService(NotificationManager::class.java)
            manager.createNotificationChannel(channel)
        }
    }

    override fun onDestroy() {
        stopServer()
        super.onDestroy()
    }
}
