using System;
using System.Collections.Generic;

namespace SystemMonitor.Models
{
    public class SystemSnapshot
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public SystemInfo SystemInfo { get; set; } = new();
        public CpuMetrics Cpu { get; set; } = new();
        public GpuMetrics Gpu { get; set; } = new();
        public MemoryMetrics Memory { get; set; } = new();
        public PowerMetrics Power { get; set; } = new();
        public List<DiskMetrics> Disks { get; set; } = new();
        public List<NetworkMetrics> NetworkInterfaces { get; set; } = new();
        public NetworkPingMetrics PingLatency { get; set; } = new();
        public List<NetworkConnectionItem> ActiveConnections { get; set; } = new();
        public List<StartupProgramItem> StartupPrograms { get; set; } = new();
        public List<ProcessItem> Processes { get; set; } = new();
        public List<SystemAlert> Alerts { get; set; } = new();
        public BenchmarkResult? LatestBenchmark { get; set; }
    }

    public class SystemInfo
    {
        public string HostName { get; set; } = string.Empty;
        public string OsDescription { get; set; } = string.Empty;
        public string OsArchitecture { get; set; } = string.Empty;
        public string CpuName { get; set; } = string.Empty;
        public int LogicalProcessorCount { get; set; }
        public long UptimeSeconds { get; set; }
        public string FrameworkVersion { get; set; } = string.Empty;
    }

    public class CpuMetrics
    {
        public float OverallUsage { get; set; }
        public List<float> CoreUsages { get; set; } = new();
        public int ThreadCount { get; set; }
        public int ProcessCount { get; set; }
    }

    public class GpuMetrics
    {
        public string Name { get; set; } = "Integrated Graphics";
        public string DriverVersion { get; set; } = "N/A";
        public double VramTotalMb { get; set; } = 4096;
        public double VramUsedMb { get; set; } = 1280;
        public float VramUsagePercentage { get; set; } = 31.2f;
        public string Status { get; set; } = "Active";
    }

    public class MemoryMetrics
    {
        public double TotalMb { get; set; }
        public double UsedMb { get; set; }
        public double FreeMb { get; set; }
        public float UsagePercentage { get; set; }
    }

    public class PowerMetrics
    {
        public bool HasBattery { get; set; }
        public bool IsAcOnline { get; set; }
        public bool IsCharging { get; set; }
        public int BatteryLifePercent { get; set; }
        public int BatteryLifeTimeSeconds { get; set; }
        public string PowerStatusText { get; set; } = "AC Power";
    }

    public class DiskMetrics
    {
        public string Name { get; set; } = string.Empty;
        public string VolumeLabel { get; set; } = string.Empty;
        public string DriveType { get; set; } = string.Empty;
        public string DriveFormat { get; set; } = string.Empty;
        public double TotalGb { get; set; }
        public double UsedGb { get; set; }
        public double FreeGb { get; set; }
        public float UsagePercentage { get; set; }
        public double ReadSpeedMBps { get; set; }
        public double WriteSpeedMBps { get; set; }
    }

    public class NetworkMetrics
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public double DownloadSpeedKbps { get; set; }
        public double UploadSpeedKbps { get; set; }
        public double TotalReceivedMb { get; set; }
        public double TotalSentMb { get; set; }
    }

    public class NetworkPingMetrics
    {
        public int PingMs { get; set; } = 14;
        public string TargetHost { get; set; } = "1.1.1.1 (Cloudflare)";
        public string Status { get; set; } = "Optimal";
        public float PacketLossPercent { get; set; } = 0.0f;
    }

    public class NetworkConnectionItem
    {
        public string LocalEndPoint { get; set; } = string.Empty;
        public string RemoteEndPoint { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Protocol { get; set; } = "TCP";
        public int Port { get; set; }
    }

    public class StartupProgramItem
    {
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }

    public class ProcessItem
    {
        public int Pid { get; set; }
        public string Name { get; set; } = string.Empty;
        public double WorkingSetMb { get; set; }
        public float CpuPercentage { get; set; }
        public int ThreadCount { get; set; }
        public string PriorityClass { get; set; } = "Normal";
        public string Status { get; set; } = "Running";
    }

    public class SystemAlert
    {
        public string Type { get; set; } = "Info";
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class BenchmarkResult
    {
        public bool IsRunning { get; set; }
        public int SingleCoreScore { get; set; }
        public int MultiCoreScore { get; set; }
        public long TotalOperations { get; set; }
        public double DurationSeconds { get; set; }
        public string ScoreRating { get; set; } = "Not Tested";
        public DateTime TestedAt { get; set; } = DateTime.UtcNow;
    }

    public class ProcessActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class FlushMemoryResult
    {
        public bool Success { get; set; }
        public double FreedMb { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class PriorityRequest
    {
        public string Priority { get; set; } = "Normal";
    }
}
