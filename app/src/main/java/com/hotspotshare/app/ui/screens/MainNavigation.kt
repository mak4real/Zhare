package com.hotspotshare.app.ui.screens

import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector

enum class Screen(val title: String, val icon: ImageVector) {
    HOME("Home", Icons.Default.WifiTethering),
    SHARE("Share", Icons.Default.UploadFile),
    RECEIVED("Received", Icons.Default.FolderOpen),
    CONNECT("Connect", Icons.Default.Phonelink)
}

@Composable
fun MainNavigation() {
    var selectedScreen by remember { mutableStateOf(Screen.HOME) }

    Scaffold(
        bottomBar = {
            NavigationBar {
                Screen.values().forEach { screen ->
                    NavigationBarItem(
                        icon = { Icon(screen.icon, contentDescription = screen.title) },
                        label = { Text(screen.title) },
                        selected = selectedScreen == screen,
                        onClick = { selectedScreen = screen }
                    )
                }
            }
        }
    ) { innerPadding ->
        val modifier = Modifier
            .fillMaxSize()
            .padding(innerPadding)

        when (selectedScreen) {
            Screen.HOME -> HomeScreen(
                modifier = modifier,
                onNavigateToSend = { selectedScreen = Screen.SHARE },
                onNavigateToReceive = { selectedScreen = Screen.RECEIVED }
            )
            Screen.SHARE -> SendScreen(modifier = modifier)
            Screen.RECEIVED -> ReceiveScreen(modifier = modifier)
            Screen.CONNECT -> ConnectScreen(modifier = modifier)
        }
    }
}
