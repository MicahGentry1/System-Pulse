# SYSTEM PULSE - Real-Time C# System Monitor

![C#](https://img.shields.io/badge/C%23-.NET%2010-purple?style=for-the-badge&logo=dotnet)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-SignalR-blue?style=for-the-badge&logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?style=for-the-badge&logo=windows)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

**SYSTEM PULSE** is a high-performance, real-time System Monitor application written in C# (.NET 10). It combines a native Windows system telemetry collector with an ASP.NET Core & SignalR backend, running inside an embedded **Microsoft Edge WebView2** native Windows desktop window.

---

## ✨ Features

- **📦 Dedicated Installer (`SystemPulseInstaller.exe`)**: Easy-to-use setup GUI wizard that extracts the app, creates Desktop & Start Menu shortcuts, and launches SYSTEM PULSE automatically.
- **🧹 Memory Flush & RAM Optimizer**: One-click memory trim calling Win32 `EmptyWorkingSet` across non-essential processes to free up physical memory.
- **🚀 Windows Startup Programs Inspector**: Scans HKCU/HKLM registry keys and startup folders to display all autostart apps and command paths.
- **⚡ Built-in Multi-Core CPU Benchmark**: Concurrent multi-threaded SHA-256 benchmark computing Single-Core and Multi-Core scores.
- **🔔 Windows System Tray & Floating Mini-Widget**: Tray background minimization with live hover metrics and `TopMost` floating mini-widget mode (`360x240`).
- **🔌 Active Network Connections Inspector**: Live tracker for active TCP/UDP sockets, remote endpoints, and listening ports.
- **💻 Native Desktop WebView Application**: `SystemMonitor.exe` runs as a native Windows desktop GUI app with an embedded WebView2 control displaying the real-time telemetry dashboard.
- **⚡ Real-Time Telemetry Engine**: Powered by C# `System.Diagnostics` and Win32 P/Invoke APIs, streaming low-latency metric updates every 1 second via SignalR WebSockets.

---

## 🛠️ Tech Stack

- **Backend**: C# (.NET 10), ASP.NET Core Kestrel, SignalR Hub, `System.Diagnostics.PerformanceCounter`, Win32 API (`GlobalMemoryStatusEx`)
- **Frontend**: HTML5, CSS3 Glassmorphism, JavaScript (ES6+), SignalR JS Client, HTML5 Canvas API
- **Tooling & Packaging**: `.NET CLI`, Inno Setup Script (`setup.iss`), PowerShell Installer (`Install.ps1`)

---

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or Windows 10/11 x64 to run pre-built binaries)

### Build & Run from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/MicahGentry1/System-Pulse.git
   cd System-Pulse
   ```

2. Run the application:
   ```bash
   dotnet run
   ```

3. Open your browser and navigate to:
   ```
   http://localhost:5200
   ```

---

## 📦 Publishing & Packaging

To compile a standalone, single-file Windows executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

The resulting `SystemMonitor.exe` will be saved in the `publish/` folder.

---

## ⚠️ Windows SmartScreen Notice (Unsigned Executable)

> [!NOTE]
> Because `SystemMonitor.exe` is an open-source build and is **not digitally signed** with a commercial Code Signing Certificate, **Windows SmartScreen / Defender** may display a standard warning screen (*"Windows protected your PC"*) when launching the pre-compiled `.exe` for the first time.

### How to Run the Pre-compiled Binary:
1. On the Windows SmartScreen popup, click **More info**.
2. Click **Run anyway**.

> **Note on Security & Transparency**: SYSTEM PULSE is **100% open-source**. All C# backend telemetry logic, Win32 API calls, and web assets are fully visible in this repository. You can audit the code or compile your own signed/unsigned binary directly from source using `dotnet publish`.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
