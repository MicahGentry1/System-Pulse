# SYSTEM PULSE - Real-Time C# System Monitor

![C#](https://img.shields.io/badge/C%23-.NET%2010-purple?style=for-the-badge&logo=dotnet)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-SignalR-blue?style=for-the-badge&logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20x64-0078D6?style=for-the-badge&logo=linux)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

**SYSTEM PULSE** is a high-performance, real-time System Monitor application written in C# (.NET 10). It combines a cross-platform system telemetry collector (Windows & Linux) with an ASP.NET Core & SignalR backend, running as a native Windows desktop app (via embedded **Microsoft Edge WebView2**) or a Linux desktop web daemon.

---

## ✨ Features

- **🐧 Cross-Platform Linux Support**: Native telemetry collector for Linux (Ubuntu, Debian, Fedora, Arch, Alpine, etc.) parsing `/proc/stat`, `/proc/meminfo`, `/sys/class/power_supply`, and `~/.config/autostart`. Includes `install-linux.sh` setup script and auto browser launch via `xdg-open`.
- **📦 Dedicated Windows Installer (`SystemPulseInstaller.exe`)**: Easy-to-use setup GUI wizard that extracts the app, creates Desktop & Start Menu shortcuts, and launches SYSTEM PULSE automatically.
- **🧹 Memory Flush & RAM Optimizer**: One-click memory trim calling Win32 `EmptyWorkingSet` across non-essential processes to free up physical memory.
- **🚀 Startup Programs Inspector**: Scans HKCU/HKLM Windows registry keys and Linux autostart desktop entries to display all autostart apps.
- **⚡ Built-in Multi-Core CPU Benchmark**: Concurrent multi-threaded SHA-256 benchmark computing Single-Core and Multi-Core scores.
- **🔔 Windows System Tray & Floating Mini-Widget**: Tray background minimization with live hover metrics and `TopMost` floating mini-widget mode (`360x240`).
- **🔌 Active Network Connections Inspector**: Live tracker for active TCP/UDP sockets, remote endpoints, and listening ports.
- **⚡ Real-Time Telemetry Engine**: Powered by C# `System.Diagnostics`, Linux `/proc`, and Win32 P/Invoke APIs, streaming low-latency metric updates every 1 second via SignalR WebSockets.

---

## 🛠️ Tech Stack

- **Backend**: C# (.NET 10), ASP.NET Core Kestrel, SignalR Hub, Linux `/proc` Telemetry, Win32 API (`GlobalMemoryStatusEx`)
- **Frontend**: HTML5, CSS3 Glassmorphism, JavaScript (ES6+), SignalR JS Client, HTML5 Canvas API
- **Tooling & Packaging**: `.NET CLI`, Windows Setup Wizard (`SystemPulseInstaller.exe`), Linux Installer Script (`install-linux.sh`)

---

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or Windows/Linux x64 OS to run pre-built binaries)

### 🐧 Installing & Running on Linux (Ubuntu, Debian, Fedora, Arch, Alpine)

1. Download the latest Linux release package: [`SystemMonitor-v3.0.1-linux-x64.zip`](https://github.com/MicahGentry1/System-Pulse/releases/tag/v3.0.1)
2. Extract and run the installer script:
   ```bash
   unzip SystemMonitor-v3.0.1-linux-x64.zip
   cd linux-x64
   chmod +x install-linux.sh SystemMonitor
   ./install-linux.sh
   ```
3. Launch `SYSTEM PULSE` from your application menu or run `systempulse` in your terminal!

---

### 🪟 Installing & Running on Windows

1. Download [`SystemPulseInstaller.exe`](https://github.com/MicahGentry1/System-Pulse/releases/tag/v3.0.1) from GitHub Releases.
2. Run `SystemPulseInstaller.exe` to install SYSTEM PULSE with Desktop & Start Menu shortcuts!

---

### 💻 Build & Run from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/MicahGentry1/System-Pulse.git
   cd System-Pulse
   ```

2. Run the application:
   ```bash
   # Windows
   dotnet run -f net10.0-windows

   # Linux
   dotnet run -f net10.0
   ```

---

## 📦 Publishing & Packaging

To compile standalone, single-file executables:

```bash
# Publish for Windows x64
dotnet publish -f net10.0-windows -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/win-x64

# Publish for Linux x64
dotnet publish -f net10.0 -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish/linux-x64
```

---

## ⚠️ Windows SmartScreen Notice (Unsigned Executable)

> [!NOTE]
> Because `SystemMonitor.exe` is an open-source build and is **not digitally signed** with a commercial Code Signing Certificate, **Windows SmartScreen / Defender** may display a standard warning screen (*"Windows protected your PC"*) when launching the pre-compiled `.exe` for the first time.

### How to Run the Pre-compiled Binary:
1. On the Windows SmartScreen popup, click **More info**.
2. Click **Run anyway**.

> **Note on Security & Transparency**: SYSTEM PULSE is **100% open-source**. All C# backend telemetry logic, Win32/Linux API calls, and web assets are fully visible in this repository. You can audit the code or compile your own signed/unsigned binary directly from source using `dotnet publish`.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
