package com.hotspotshare.app.network

import android.content.Context
import android.net.wifi.WifiManager
import android.os.Build
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import org.json.JSONObject
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.SocketException
import java.util.concurrent.ConcurrentHashMap

data class DiscoveredDevice(
    val id: String,
    val name: String,
    val type: String, // "windows" or "android"
    val ip: String,
    val port: Int = 8888,
    val lastSeen: Long = System.currentTimeMillis()
)

object DiscoveryManager {

    private const val DISCOVERY_PORT = 8889
    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
    private var multicastLock: WifiManager.MulticastLock? = null

    private val deviceMap = ConcurrentHashMap<String, DiscoveredDevice>()
    private val _discoveredDevices = MutableStateFlow<List<DiscoveredDevice>>(emptyList())
    val discoveredDevices: StateFlow<List<DiscoveredDevice>> = _discoveredDevices.asStateFlow()

    private var isRunning = false
    private var listenSocket: DatagramSocket? = null

    fun startDiscovery(context: Context) {
        if (isRunning) return
        isRunning = true

        try {
            val wifi = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager
            multicastLock = wifi?.createMulticastLock("HotspotShareDiscovery")
            multicastLock?.setReferenceCounted(true)
            multicastLock?.acquire()
        } catch (e: Exception) {
            e.printStackTrace()
        }

        // 1. Listen for UDP broadcasts
        scope.launch {
            listenForBeacons()
        }

        // 2. Broadcast own beacon every 2 seconds
        scope.launch {
            broadcastBeacons()
        }

        // 3. Prune inactive devices every 4 seconds
        scope.launch {
            while (isRunning) {
                delay(4000)
                val now = System.currentTimeMillis()
                val iterator = deviceMap.entries.iterator()
                var changed = false
                while (iterator.hasNext()) {
                    val entry = iterator.next()
                    if (now - entry.value.lastSeen > 8000) {
                        iterator.remove()
                        changed = true
                    }
                }
                if (changed) {
                    _discoveredDevices.value = deviceMap.values.toList()
                }
            }
        }
    }

    private suspend fun listenForBeacons() {
        withContext(Dispatchers.IO) {
            try {
                listenSocket = DatagramSocket(DISCOVERY_PORT).apply {
                    broadcast = true
                    reuseAddress = true
                }
                val buffer = ByteArray(2048)

                while (isRunning) {
                    val packet = DatagramPacket(buffer, buffer.size)
                    try {
                        listenSocket?.receive(packet)
                        val message = String(packet.data, 0, packet.length, Charsets.UTF_8)
                        val senderIp = packet.address.hostAddress ?: continue

                        // Don't discover self
                        val localIp = NetworkUtils.getLocalIpAddress()
                        if (senderIp == localIp || senderIp == "127.0.0.1") continue

                        if (message.contains("HOTSPOT_SHARE_BEACON")) {
                            handleBeacon(message, senderIp)
                        }
                    } catch (e: SocketException) {
                        break
                    } catch (e: Exception) {
                        e.printStackTrace()
                    }
                }
            } catch (e: Exception) {
                e.printStackTrace()
            }
        }
    }

    private fun handleBeacon(jsonStr: String, senderIp: String) {
        try {
            val json = JSONObject(jsonStr)
            val name = json.optString("deviceName", "Device")
            val type = json.optString("deviceType", "device")
            val port = json.optInt("port", 8888)
            val key = "$senderIp:$port"

            val device = DiscoveredDevice(
                id = key,
                name = name,
                type = type,
                ip = senderIp,
                port = port,
                lastSeen = System.currentTimeMillis()
            )

            deviceMap[key] = device
            _discoveredDevices.value = deviceMap.values.toList()
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }

    private suspend fun broadcastBeacons() {
        withContext(Dispatchers.IO) {
            while (isRunning) {
                try {
                    val socket = DatagramSocket().apply { broadcast = true }
                    val beaconJson = JSONObject().apply {
                        put("type", "HOTSPOT_SHARE_BEACON")
                        put("deviceName", "${Build.MANUFACTURER.replaceFirstChar { it.uppercase() }} ${Build.MODEL}")
                        put("deviceType", "android")
                        put("port", NetworkUtils.DEFAULT_PORT)
                    }.toString()

                    val bytes = beaconJson.toByteArray(Charsets.UTF_8)

                    // 1. Global broadcast
                    try {
                        val packet = DatagramPacket(bytes, bytes.size, InetAddress.getByName("255.255.255.255"), DISCOVERY_PORT)
                        socket.send(packet)
                    } catch (e: Exception) { }

                    // 2. Subnet directed broadcasts (for home/office Wi-Fi routers)
                    try {
                        val interfaces = java.util.Collections.list(java.net.NetworkInterface.getNetworkInterfaces())
                        for (intf in interfaces) {
                            if (!intf.isLoopback && intf.isUp) {
                                for (addr in intf.interfaceAddresses) {
                                    val bcast = addr.broadcast
                                    if (bcast != null) {
                                        val p = DatagramPacket(bytes, bytes.size, bcast, DISCOVERY_PORT)
                                        socket.send(p)
                                    }
                                }
                            }
                        }
                    } catch (e: Exception) { }

                    socket.close()
                } catch (e: Exception) {
                    // Ignore broadcast send errors when network switching
                }
                delay(2000)
            }
        }
    }

    fun stopDiscovery() {
        isRunning = false
        try {
            listenSocket?.close()
            listenSocket = null
        } catch (e: Exception) { }
        try {
            multicastLock?.release()
            multicastLock = null
        } catch (e: Exception) { }
        deviceMap.clear()
        _discoveredDevices.value = emptyList()
    }
}
