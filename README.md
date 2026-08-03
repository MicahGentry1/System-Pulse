# SYSTEM PULSE - Real-Time C# System Monitor

![C#](https://img.shields.io/badge/C%23-.NET%2010-purple?style=for-the-badge&logo=dotnet)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-SignalR-blue?style=for-the-badge&logo=dotnet)
![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?style=for-the-badge&logo=windows)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

**SYSTEM PULSE** is a high-performance, real-time System Monitor application written in C# (.NET 10). It combines a native Windows system telemetry collector with an ASP.NET Core & SignalR backend, running inside an embedded **Microsoft Edge WebView2** native Windows desktop window.

---

## ✨ Features

- **💻 Native Desktop WebView Application**: `SystemMonitor.exe` runs as a native Windows desktop GUI app with an embedded WebView2 control displaying the real-time telemetry dashboard.
- **⚡ Real-Time Telemetry Engine**: Powered by C# `System.Diagnostics` and Win32 P/Invoke APIs, streaming low-latency metric updates every 1 second via SignalR WebSockets.
- **🖥️ CPU & Logical Core Matrix**: Visualizes overall CPU percentage, thread counts, total process count, and individual load bars for every logical core (Core 0 to Core N).
- **🧠 Memory (RAM) Monitor**: High-precision physical RAM breakdown (Used, Free, Total GB, and Usage Percentage).
- **💾 Storage Drive Inspector**: Monitors all mounted storage drives (`C:\`, `D:\`), volume labels, file system types (NTFS/ReFS), and used space percentages.
- **🌐 Network Bandwidth Tracker**: Measures active network adapters, IP addresses, and real-time Download / Upload throughput speeds (KB/s & MB/s).
- **📊 Interactive Process Manager**: Live process list with search filtering, sorting (by RAM, CPU %, Name, PID), and an interactive **End Task** termination engine.
- **🎨 Glassmorphic Dark UI**: Built with custom HTML5 Canvas time-series charts, smooth micro-animations, neon status indicators, and responsive CSS grid layout.
- **📦 Standalone Packaging**: Can be compiled into a single-file `SystemMonitor.exe` that runs standalone on Windows without requiring pre-installed .NET runtimes.

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
   git clone https://github.com/YOUR_USERNAME/SystemMonitor.git
   cd SystemMonitor
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

## 📄 License

This project is licensed under the [MIT License](LICENSE).
