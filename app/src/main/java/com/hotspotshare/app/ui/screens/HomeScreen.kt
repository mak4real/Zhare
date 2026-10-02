package com.hotspotshare.app.ui.screens

import android.content.Context
import android.widget.Toast
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.*
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.hotspotshare.app.network.DiscoveryManager
import com.hotspotshare.app.network.NetworkUtils
import com.hotspotshare.app.service.TransferForegroundService

@Composable
fun HomeScreen(
    modifier: Modifier = Modifier,
    onNavigateToSend: () -> Unit = {},
    onNavigateToReceive: () -> Unit = {}
) {
    val context = LocalContext.current
    val isRunning by TransferForegroundService.isRunning.collectAsState()
    val sharedFiles by TransferForegroundService.sharedFiles.collectAsState()
    val receivedFiles by TransferForegroundService.receivedFiles.collectAsState()
    val discoveredDevices by DiscoveryManager.discoveredDevices.collectAsState()

    var localIp by remember { mutableStateOf(NetworkUtils.getLocalIpAddress() ?: "192.168.43.1") }
    val wifiSsid by remember { mutableStateOf(NetworkUtils.getWifiSsid(context)) }

    LaunchedEffect(isRunning) {
        localIp = NetworkUtils.getLocalIpAddress() ?: "192.168.43.1"
    }

    val infiniteTransition = rememberInfiniteTransition(label = "pulse")
    val pulseScale by infiniteTransition.animateFloat(
        initialValue = 1.0f,
        targetValue = 1.25f,
        animationSpec = infiniteRepeatable(
            animation = tween(1200, easing = FastOutSlowInEasing),
            repeatMode = RepeatMode.Reverse
        ),
        label = "pulseScale"
    )

    Column(
        modifier = modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(20.dp),
        verticalArrangement = Arrangement.spacedBy(20.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        // Simple Clean Header
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Column {
                Text(
                    text = "ZHARE",
                    fontSize = 24.sp,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onBackground
                )
                Text(
                    text = if (isRunning) "SERVER ACTIVE" else "CONNECT TO PC",
                    fontSize = 13.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }

            IconButton(
                onClick = {
                    try {
                        context.startActivity(NetworkUtils.getTetheringSettingsIntent())
                    } catch (e: Exception) {
                        try {
                            context.startActivity(NetworkUtils.getWirelessSettingsIntent())
                        } catch (e2: Exception) {
                            Toast.makeText(context, "Open Hotspot in Phone Settings", Toast.LENGTH_SHORT).show()
                        }
                    }
                }
            ) {
                Icon(
                    imageVector = Icons.Default.WifiTethering,
                    contentDescription = "Hotspot Settings",
                    tint = MaterialTheme.colorScheme.onBackground
                )
            }
        }

        // Hero Beacon Card
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(
                containerColor = MaterialTheme.colorScheme.background
            ),
            shape = RoundedCornerShape(0.dp),
            border = BorderStroke(3.dp, MaterialTheme.colorScheme.onBackground)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(28.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                Box(
                    modifier = Modifier.size(90.dp),
                    contentAlignment = Alignment.Center
                ) {
                    if (isRunning) {
                        Surface(
                            modifier = Modifier
                                .size(90.dp)
                                .scale(pulseScale),
                            shape = RoundedCornerShape(0.dp),
                            color = MaterialTheme.colorScheme.primary.copy(alpha = 0.2f),
                            border = BorderStroke(2.dp, MaterialTheme.colorScheme.primary)
                        ) {}
                    }

                    Surface(
                        modifier = Modifier.size(64.dp),
                        shape = RoundedCornerShape(0.dp),
                        color = if (isRunning) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.background,
                        border = BorderStroke(if (isRunning) 0.dp else 2.dp, MaterialTheme.colorScheme.onBackground)
                    ) {
                        Box(contentAlignment = Alignment.Center) {
                            Icon(
                                imageVector = if (isRunning) Icons.Default.Bolt else Icons.Default.PowerSettingsNew,
                                contentDescription = null,
                                modifier = Modifier.size(32.dp),
                                tint = if (isRunning) MaterialTheme.colorScheme.background else MaterialTheme.colorScheme.onBackground
                            )
                        }
                    }
                }

                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Text(
                        text = if (isRunning) "SERVER ACTIVE" else "READY TO START",
                        fontSize = 18.sp,
                        fontWeight = FontWeight.Bold
                    )
                    Text(
                        text = if (isRunning) "WIFI: $wifiSsid" else "CONNECT PC AND PHONE",
                        fontSize = 13.sp,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }

                if (isRunning) {
                    Surface(
                        color = MaterialTheme.colorScheme.background,
                        shape = RoundedCornerShape(0.dp),
                        border = BorderStroke(2.dp, MaterialTheme.colorScheme.primary)
                    ) {
                        Row(
                            modifier = Modifier.padding(horizontal = 14.dp, vertical = 6.dp),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            Text(
                                text = "IP: $localIp",
                                fontWeight = FontWeight.Bold,
                                fontSize = 15.sp,
                                color = MaterialTheme.colorScheme.primary
                            )
                        }
                    }
                }

                Button(
                    onClick = {
                        if (isRunning) {
                            TransferForegroundService.stopService(context)
                        } else {
                            TransferForegroundService.startService(context)
                        }
                    },
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(52.dp),
                    shape = RoundedCornerShape(0.dp),
                    colors = ButtonDefaults.buttonColors(
                        containerColor = if (isRunning) MaterialTheme.colorScheme.background else MaterialTheme.colorScheme.onBackground,
                        contentColor = if (isRunning) MaterialTheme.colorScheme.onBackground else MaterialTheme.colorScheme.background
                    ),
                    border = BorderStroke(2.dp, MaterialTheme.colorScheme.onBackground)
                ) {
                    Text(
                        text = if (isRunning) "STOP SERVER" else "START SERVER",
                        fontSize = 15.sp,
                        fontWeight = FontWeight.Bold
                    )
                }
            }
        }

        AnimatedVisibility(visible = isRunning) {
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(0.dp),
                border = BorderStroke(2.dp, MaterialTheme.colorScheme.onBackground),
                colors = CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.background
                )
            ) {
                Column(
                    modifier = Modifier.padding(18.dp),
                    verticalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    Text(
                        text = "CONNECTED DEVICES",
                        fontWeight = FontWeight.Bold,
                        fontSize = 14.sp
                    )

                    if (discoveredDevices.isNotEmpty()) {
                        discoveredDevices.forEach { dev ->
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .background(MaterialTheme.colorScheme.background)
                                    .padding(12.dp),
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(12.dp)
                            ) {
                                Icon(
                                    imageVector = if (dev.type == "windows") Icons.Default.Laptop else Icons.Default.PhoneAndroid,
                                    contentDescription = null,
                                    tint = MaterialTheme.colorScheme.primary
                                )
                                Column(modifier = Modifier.weight(1f)) {
                                    Text(dev.name.uppercase(), fontWeight = FontWeight.Bold, fontSize = 14.sp)
                                    Text("${dev.ip} • READY", fontSize = 12.sp, color = MaterialTheme.colorScheme.primary)
                                }
                            }
                        }
                    } else {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(10.dp)
                        ) {
                            CircularProgressIndicator(modifier = Modifier.size(16.dp), strokeWidth = 2.dp, color = MaterialTheme.colorScheme.primary)
                            Text(
                                text = "AWAITING PC CONNECTION...",
                                fontSize = 13.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                }
            }
        }

        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            Card(
                modifier = Modifier.weight(1f),
                onClick = onNavigateToSend,
                shape = RoundedCornerShape(0.dp),
                border = BorderStroke(2.dp, MaterialTheme.colorScheme.onBackground),
                colors = CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.background
                )
            ) {
                Column(
                    modifier = Modifier.padding(20.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.UploadFile,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onBackground,
                        modifier = Modifier.size(28.dp)
                    )
                    Text("SEND", fontWeight = FontWeight.Bold, fontSize = 16.sp)
                    Text("${sharedFiles.size} QUEUED", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }

            Card(
                modifier = Modifier.weight(1f),
                onClick = onNavigateToReceive,
                shape = RoundedCornerShape(0.dp),
                border = BorderStroke(2.dp, MaterialTheme.colorScheme.onBackground),
                colors = CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.background
                )
            ) {
                Column(
                    modifier = Modifier.padding(20.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.FolderOpen,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onBackground,
                        modifier = Modifier.size(28.dp)
                    )
                    Text("RECEIVE", fontWeight = FontWeight.Bold, fontSize = 16.sp)
                    Text("${receivedFiles.size} FILES", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }
    }
}
