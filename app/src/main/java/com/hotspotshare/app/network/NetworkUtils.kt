package com.hotspotshare.app.network

import android.content.Context
import android.content.Intent
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.net.wifi.WifiManager
import android.provider.Settings
import java.net.Inet4Address
import java.net.NetworkInterface
import java.util.Collections

object NetworkUtils {

    const val DEFAULT_PORT = 8888

    /**
     * Resolves the primary IPv4 address of the device.
     * Prioritizes Hotspot interfaces (ap0, softap), then Wi-Fi (wlan0), then any valid IPv4.
     */
    fun getLocalIpAddress(): String? {
        try {
            val interfaces = Collections.list(NetworkInterface.getNetworkInterfaces())

            // Priority 1: Hotspot interfaces
            for (intf in interfaces) {
                val name = intf.name.lowercase()
                if (name.contains("ap") || name.contains("softap") || name.contains("hotspot")) {
                    for (addr in Collections.list(intf.inetAddresses)) {
                        if (!addr.isLoopbackAddress && addr is Inet4Address) {
                            val ip = addr.hostAddress
                            if (!ip.isNullOrEmpty() && !ip.startsWith("127.")) {
                                return ip
                            }
                        }
                    }
                }
            }

            // Priority 2: Wi-Fi interfaces (wlan)
            for (intf in interfaces) {
                val name = intf.name.lowercase()
                if (name.contains("wlan")) {
                    for (addr in Collections.list(intf.inetAddresses)) {
                        if (!addr.isLoopbackAddress && addr is Inet4Address) {
                            val ip = addr.hostAddress
                            if (!ip.isNullOrEmpty() && !ip.startsWith("127.")) {
                                return ip
                            }
                        }
                    }
                }
            }

            // Priority 3: Any non-loopback IPv4 address
            for (intf in interfaces) {
                for (addr in Collections.list(intf.inetAddresses)) {
                    if (!addr.isLoopbackAddress && addr is Inet4Address) {
                        val ip = addr.hostAddress
                        if (!ip.isNullOrEmpty() && !ip.startsWith("127.")) {
                            return ip
                        }
                    }
                }
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }
        return null
    }

    /**
     * Checks if device is connected to Wi-Fi or has an active network connection.
     */
    fun isNetworkAvailable(context: Context): Boolean {
        val cm = context.getSystemService(Context.CONNECTIVITY_SERVICE) as? ConnectivityManager ?: return false
        val activeNetwork = cm.activeNetwork ?: return false
        val capabilities = cm.getNetworkCapabilities(activeNetwork) ?: return false
        return capabilities.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) ||
                capabilities.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET)
    }

    /**
     * Gets Wi-Fi SSID if connected as a client.
     */
    fun getWifiSsid(context: Context): String {
        try {
            val wifiManager = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager
            val info = wifiManager?.connectionInfo
            val ssid = info?.ssid
            if (!ssid.isNullOrEmpty() && ssid != "<unknown ssid>") {
                return ssid.replace("\"", "")
            }
        } catch (e: Exception) {
            e.printStackTrace()
        }
        return "Hotspot / Local Wi-Fi"
    }

    /**
     * Generates Intent to open Tethering / Portable Hotspot settings.
     */
    fun getTetheringSettingsIntent(): Intent {
        val intent = Intent()
        intent.action = "android.settings.TETHER_SETTINGS"
        return intent
    }

    /**
     * Fallback Intent for Wireless Settings if tethering intent cannot be resolved.
     */
    fun getWirelessSettingsIntent(): Intent {
        return Intent(Settings.ACTION_WIRELESS_SETTINGS)
    }
}
