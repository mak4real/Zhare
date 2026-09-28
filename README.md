# Zhare ⚡

> **High-speed, zero-cloud peer-to-peer file transfer system between Android and Windows PC over local Wi-Fi or Mobile Hotspot.**

[![Platform](https://img.shields.io/badge/Platform-Android%20%7C%20Windows-blue.svg)](https://github.com/mak4real/Zhare)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Zero Cloud](https://img.shields.io/badge/Cloud-Zero%20Data%20Used-emerald.svg)]()
[![Transfer](https://img.shields.io/badge/Speed-Up%20to%2060%2B%20MB%2Fs-cyan.svg)]()

Zhare connects your **Android smartphone** and **Windows PC** directly over your local home/office Wi-Fi or phone hotspot. It streams files straight to the receiving socket with zero intermediate cloud servers, zero file compression, and unlimited transfer sizes.

---

## ✨ Features

- **⚡ Blazing Fast Local Transfer**: Transfers at full Wi-Fi speeds (30–60+ MB/s) without consuming cellular data or internet bandwidth.
- **🚀 Zero-Memory Direct TCP Streaming**:
  - Files stream directly from disk to the network in 256 KB chunk buffers without buffering into RAM.
  - Handles massive 4K video files, movies, and game archives (10GB+) with under 30 MB PC RAM usage.
- **🔍 Instant Auto-Discovery**:
  - Listens for UDP beacon broadcasts (`port 8889`) to detect nearby phones automatically.
  - Non-blocking socket scanner probes subnet gateways and active devices in seconds.
- **💎 Bespoke Cyber-Minimalist Windows UI**:
  - Cosmic obsidian background with ambient radial indigo lighting.
  - AirDrop-inspired Dynamic Device Status Capsule with live beacon pulse.
  - Interactive neon beam drop zone with luminous drag-over illumination.
  - Hardware-accelerated progress bar with live throughput telemetry (`⚡ MB/s`).
  - 100% custom WPF control templates (no Win32/default Windows controls).
- **📱 Modern Jetpack Compose Android Client**:
  - Material 3 dark design with real-time transfer state.
  - Embedded high-performance NanoHTTPD server (`port 8888`).
  - Foreground transfer service with persistent status notification.
  - Automatic download organization in `Downloads/HotspotShare`.

---

## 🚀 Getting Started

### 1. Same Wi-Fi Network Mode (Recommended)
1. Connect your **Android phone** and **Windows PC** to the same Wi-Fi router.
2. Open **Zhare** on your phone and tap **Start Server**.
3. Launch **`Zhare.exe`** on your PC.
4. The PC app will automatically detect your phone (e.g. `📱 Xiaomi • 192.168.x.x`).
5. Drag and drop any files into the drop zone and click **⚡ Beam Files to Phone**!

### 2. Mobile Hotspot Mode (No Router)
1. Turn on **Mobile Hotspot** on your phone.
2. Connect your Windows PC to your phone's Wi-Fi hotspot.
3. Open **Zhare** on both devices—they will pair automatically.

---

## 🛠️ Architecture & Technical Specs

| Component | Technology | Details |
|---|---|---|
| **Windows Client** | C# 5 / WPF (.NET Framework 4.0/4.8) | Standalone executable, direct TCP streaming, custom ControlTemplates |
| **Android Client** | Kotlin / Jetpack Compose / Coroutines | Android 8.0+ (API 26–35), Material Design 3, Foreground Service |
| **HTTP Engine** | NanoHTTPD | Lightweight embedded HTTP/1.1 file server on `0.0.0.0:8888` |
| **Discovery** | UDP Broadcast | Beacon on `0.0.0.0:8889` + non-blocking socket polling |
| **Transfer Protocol** | Multipart Stream over TCP | Zero in-memory buffering, 256 KB chunked streaming |

---

## 🔨 Building from Source

### Windows PC Client
Compile the standalone executable in one command using the native .NET C# compiler (no Visual Studio installation required):

```powershell
$wpfDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

& $csc /target:winexe /optimize+ /r:"$wpfDir\PresentationFramework.dll","$wpfDir\PresentationCore.dll","$wpfDir\WindowsBase.dll",System.dll,System.Xaml.dll /out:"Zhare.exe" "windows-pc\HotspotShare.cs"
```

### Android App
Build the APK using the Gradle wrapper:

```bash
./gradlew assembleDebug
```
The output APK will be generated at:
`app/build/outputs/apk/debug/app-debug.apk`

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
