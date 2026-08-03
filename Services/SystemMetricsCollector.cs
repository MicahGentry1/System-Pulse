using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SystemMonitor.Models;

namespace SystemMonitor.Services
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class SystemMetricsCollector
    {
        private readonly List<PerformanceCounter> _coreCounters = new();
        private PerformanceCounter? _overallCpuCounter;
        private readonly Dictionary<string, (long bytesRecv, long bytesSent, DateTime time)> _networkPrevStats = new();
        private readonly Dictionary<int, (TimeSpan cpuTime, DateTime time)> _processPrevStats = new();
        private bool _isPerformanceCounterAvailable = false;
        private readonly string _cpuName = string.Empty;

        private BenchmarkResult? _lastBenchmark;
        private bool _isBenchmarkRunning = false;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

        public SystemMetricsCollector()
        {
            InitCpuCounters();
            _cpuName = GetCpuNameFromRegistry();
        }

        private void InitCpuCounters()
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    _overallCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    _overallCpuCounter.NextValue();

                    int coreCount = Environment.ProcessorCount;
                    for (int i = 0; i < coreCount; i++)
                    {
                        var counter = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
                        counter.NextValue();
                        _coreCounters.Add(counter);
                    }
                    _isPerformanceCounterAvailable = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MetricsCollector] Warning: PerformanceCounter init failed ({ex.Message}).");
                    _isPerformanceCounterAvailable = false;
                }
            }
        }

        private string GetCpuNameFromRegistry()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                    if (key != null)
                    {
                        var name = key.GetValue("ProcessorNameString") as string;
                        if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
                    }
                }
            }
            catch { }
            return $"{Environment.ProcessorCount}-Core Processor";
        }

        public SystemSnapshot CollectSnapshot()
        {
            var snapshot = new SystemSnapshot
            {
                Timestamp = DateTime.UtcNow,
                SystemInfo = GetSystemInfo(),
                Cpu = GetCpuMetrics(),
                Memory = GetMemoryMetrics(),
                Power = GetPowerMetrics(),
                Disks = GetDiskMetrics(),
                NetworkInterfaces = GetNetworkMetrics(),
                ActiveConnections = GetActiveNetworkConnections(),
                Processes = GetTopProcesses(80),
                LatestBenchmark = _lastBenchmark
            };

            snapshot.Alerts = EvaluateAlerts(snapshot);
            return snapshot;
        }

        public async Task<BenchmarkResult> RunCpuBenchmarkAsync(int durationSeconds = 4)
        {
            if (_isBenchmarkRunning)
            {
                return _lastBenchmark ?? new BenchmarkResult { IsRunning = true };
            }

            _isBenchmarkRunning = true;
            _lastBenchmark = new BenchmarkResult { IsRunning = true, TestedAt = DateTime.UtcNow };

            int coreCount = Environment.ProcessorCount;
            int halfDurationMs = (durationSeconds * 1000) / 2;

            // 1. Single-Core Benchmark
            long singleOps = 0;
            var swSingle = Stopwatch.StartNew();
            while (swSingle.ElapsedMilliseconds < halfDurationMs)
            {
                for (int i = 0; i < 50000; i++)
                {
                    double dummy = Math.Sqrt(i) * Math.Sin(i);
                }
                singleOps += 50000;
            }
            swSingle.Stop();

            int singleScore = (int)((singleOps / Math.Max(0.1, swSingle.Elapsed.TotalSeconds)) / 20000);

            // 2. Multi-Core Benchmark
            long multiOps = 0;
            var swMulti = Stopwatch.StartNew();
            var tasks = new Task[coreCount];

            for (int t = 0; t < coreCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    long localOps = 0;
                    var sw = Stopwatch.StartNew();
                    while (sw.ElapsedMilliseconds < halfDurationMs)
                    {
                        for (int i = 0; i < 50000; i++)
                        {
                            double dummy = Math.Sqrt(i) * Math.Sin(i);
                        }
                        localOps += 50000;
                    }
                    Interlocked.Add(ref multiOps, localOps);
                });
            }

            await Task.WhenAll(tasks);
            swMulti.Stop();

            int multiScore = (int)((multiOps / Math.Max(0.1, swMulti.Elapsed.TotalSeconds)) / 20000);

            string rating = multiScore > 15000 ? "Extreme Tier" : multiScore > 8000 ? "High Performance" : "Standard Core";

            _lastBenchmark = new BenchmarkResult
            {
                IsRunning = false,
                SingleCoreScore = singleScore,
                MultiCoreScore = multiScore,
                TotalOperations = singleOps + multiOps,
                DurationSeconds = durationSeconds,
                ScoreRating = rating,
                TestedAt = DateTime.UtcNow
            };

            _isBenchmarkRunning = false;
            return _lastBenchmark;
        }

        private SystemInfo GetSystemInfo()
        {
            long uptimeSeconds = Environment.TickCount64 / 1000;
            return new SystemInfo
            {
                HostName = Environment.MachineName,
                OsDescription = RuntimeInformation.OSDescription,
                OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                CpuName = _cpuName,
                LogicalProcessorCount = Environment.ProcessorCount,
                UptimeSeconds = uptimeSeconds,
                FrameworkVersion = RuntimeInformation.FrameworkDescription
            };
        }

        private CpuMetrics GetCpuMetrics()
        {
            var cpu = new CpuMetrics();
            int totalProcesses = 0;
            int totalThreads = 0;

            try
            {
                var allProcs = Process.GetProcesses();
                totalProcesses = allProcs.Length;
                foreach (var p in allProcs)
                {
                    try { totalThreads += p.Threads.Count; } catch { }
                }
            }
            catch { }

            cpu.ProcessCount = totalProcesses;
            cpu.ThreadCount = totalThreads;

            if (_isPerformanceCounterAvailable && _overallCpuCounter != null)
            {
                try
                {
                    float totalUsage = _overallCpuCounter.NextValue();
                    cpu.OverallUsage = MathF.Min(100f, MathF.Max(0f, totalUsage));

                    foreach (var counter in _coreCounters)
                    {
                        float val = counter.NextValue();
                        cpu.CoreUsages.Add(MathF.Min(100f, MathF.Max(0f, val)));
                    }
                    return cpu;
                }
                catch { }
            }

            cpu.OverallUsage = 15.0f;
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                cpu.CoreUsages.Add(10.0f + (i * 2) % 30);
            }
            return cpu;
        }

        private MemoryMetrics GetMemoryMetrics()
        {
            var mem = new MemoryMetrics();

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var memStatus = new MEMORYSTATUSEX();
                    if (GlobalMemoryStatusEx(memStatus))
                    {
                        double totalMb = memStatus.ullTotalPhys / (1024.0 * 1024.0);
                        double availMb = memStatus.ullAvailPhys / (1024.0 * 1024.0);
                        double usedMb = totalMb - availMb;

                        mem.TotalMb = Math.Round(totalMb, 1);
                        mem.FreeMb = Math.Round(availMb, 1);
                        mem.UsedMb = Math.Round(usedMb, 1);
                        mem.UsagePercentage = (float)Math.Round((usedMb / totalMb) * 100.0, 1);
                        return mem;
                    }
                }
                catch { }
            }

            mem.TotalMb = 16384;
            mem.UsedMb = 8192;
            mem.FreeMb = 8192;
            mem.UsagePercentage = 50.0f;
            return mem;
        }

        private PowerMetrics GetPowerMetrics()
        {
            var power = new PowerMetrics();
            try
            {
                if (GetSystemPowerStatus(out var sps))
                {
                    power.IsAcOnline = sps.ACLineStatus == 1;
                    power.HasBattery = sps.BatteryFlag != 128 && sps.BatteryLifePercent != 255;
                    power.BatteryLifePercent = sps.BatteryLifePercent <= 100 ? sps.BatteryLifePercent : 100;
                    power.IsCharging = (sps.BatteryFlag & 8) != 0;
                    power.BatteryLifeTimeSeconds = sps.BatteryLifeTime;

                    if (!power.HasBattery)
                    {
                        power.PowerStatusText = "Desktop (AC Power)";
                    }
                    else if (power.IsCharging)
                    {
                        power.PowerStatusText = $"Charging ({power.BatteryLifePercent}%)";
                    }
                    else if (power.IsAcOnline)
                    {
                        power.PowerStatusText = $"Plugged In ({power.BatteryLifePercent}%)";
                    }
                    else
                    {
                        power.PowerStatusText = $"On Battery ({power.BatteryLifePercent}%)";
                    }
                }
            }
            catch { }
            return power;
        }

        private List<DiskMetrics> GetDiskMetrics()
        {
            var list = new List<DiskMetrics>();
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    try
                    {
                        double totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                        double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                        double usedGb = totalGb - freeGb;
                        float usagePct = totalGb > 0 ? (float)((usedGb / totalGb) * 100.0) : 0f;

                        list.Add(new DiskMetrics
                        {
                            Name = drive.Name,
                            VolumeLabel = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel,
                            DriveType = drive.DriveType.ToString(),
                            DriveFormat = drive.DriveFormat,
                            TotalGb = Math.Round(totalGb, 1),
                            UsedGb = Math.Round(usedGb, 1),
                            FreeGb = Math.Round(freeGb, 1),
                            UsagePercentage = (float)Math.Round(usagePct, 1)
                        });
                    }
                    catch { }
                }
            }
            catch { }
            return list;
        }

        private List<NetworkMetrics> GetNetworkMetrics()
        {
            var list = new List<NetworkMetrics>();
            DateTime now = DateTime.UtcNow;

            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    var stats = ni.GetIPStatistics();
                    long bytesRecv = stats.BytesReceived;
                    long bytesSent = stats.BytesSent;

                    double downSpeed = 0;
                    double upSpeed = 0;

                    if (_networkPrevStats.TryGetValue(ni.Id, out var prev))
                    {
                        double elapsedSec = (now - prev.time).TotalSeconds;
                        if (elapsedSec > 0.2)
                        {
                            long diffRecv = Math.Max(0, bytesRecv - prev.bytesRecv);
                            long diffSent = Math.Max(0, bytesSent - prev.bytesSent);

                            downSpeed = (diffRecv / 1024.0) / elapsedSec;
                            upSpeed = (diffSent / 1024.0) / elapsedSec;
                        }
                    }

                    _networkPrevStats[ni.Id] = (bytesRecv, bytesSent, now);

                    string ipAddress = "";
                    try
                    {
                        var ipProps = ni.GetIPProperties();
                        foreach (var ip in ipProps.UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                ipAddress = ip.Address.ToString();
                                break;
                            }
                        }
                    }
                    catch { }

                    list.Add(new NetworkMetrics
                    {
                        Name = ni.Name,
                        Description = ni.Description,
                        IpAddress = ipAddress,
                        DownloadSpeedKbps = Math.Round(downSpeed, 1),
                        UploadSpeedKbps = Math.Round(upSpeed, 1),
                        TotalReceivedMb = Math.Round(bytesRecv / (1024.0 * 1024.0), 1),
                        TotalSentMb = Math.Round(bytesSent / (1024.0 * 1024.0), 1)
                    });
                }
            }
            catch { }

            return list;
        }

        private List<NetworkConnectionItem> GetActiveNetworkConnections()
        {
            var list = new List<NetworkConnectionItem>();
            try
            {
                var ipProps = IPGlobalProperties.GetIPGlobalProperties();
                var tcpConns = ipProps.GetActiveTcpConnections();
                foreach (var conn in tcpConns.Take(50))
                {
                    string remote = conn.RemoteEndPoint.Address.ToString();
                    if (remote == "0.0.0.0" || remote == "::") remote = "Listening";
                    else remote = $"{remote}:{conn.RemoteEndPoint.Port}";

                    list.Add(new NetworkConnectionItem
                    {
                        LocalEndPoint = $"{conn.LocalEndPoint.Address}:{conn.LocalEndPoint.Port}",
                        RemoteEndPoint = remote,
                        State = conn.State.ToString(),
                        Protocol = "TCP",
                        Port = conn.LocalEndPoint.Port
                    });
                }
            }
            catch { }
            return list;
        }

        private List<ProcessItem> GetTopProcesses(int count)
        {
            var list = new List<ProcessItem>();
            DateTime now = DateTime.UtcNow;
            int coreCount = Environment.ProcessorCount;

            try
            {
                var processes = Process.GetProcesses();
                foreach (var p in processes)
                {
                    try
                    {
                        int pid = p.Id;
                        string name = p.ProcessName;
                        double memoryMb = Math.Round(p.WorkingSet64 / (1024.0 * 1024.0), 1);
                        int threads = 0;
                        string priorityStr = "Normal";

                        try { threads = p.Threads.Count; } catch { }
                        try { priorityStr = p.PriorityClass.ToString(); } catch { }

                        float cpuPct = 0f;
                        try
                        {
                            TimeSpan cpuTime = p.TotalProcessorTime;
                            if (_processPrevStats.TryGetValue(pid, out var prev))
                            {
                                double timeElapsedSec = (now - prev.time).TotalSeconds;
                                double cpuTimeSec = (cpuTime - prev.cpuTime).TotalSeconds;

                                if (timeElapsedSec > 0.2)
                                {
                                    cpuPct = (float)((cpuTimeSec / timeElapsedSec / coreCount) * 100.0);
                                    cpuPct = MathF.Min(100f, MathF.Max(0f, cpuPct));
                                }
                            }
                            _processPrevStats[pid] = (cpuTime, now);
                        }
                        catch { }

                        list.Add(new ProcessItem
                        {
                            Pid = pid,
                            Name = name,
                            WorkingSetMb = memoryMb,
                            CpuPercentage = (float)Math.Round(cpuPct, 1),
                            ThreadCount = threads,
                            PriorityClass = priorityStr
                        });
                    }
                    catch { }
                }
            }
            catch { }

            return list.OrderByDescending(p => p.WorkingSetMb).Take(count).ToList();
        }

        private List<SystemAlert> EvaluateAlerts(SystemSnapshot snap)
        {
            var alerts = new List<SystemAlert>();

            if (snap.Cpu.OverallUsage > 85.0f)
            {
                alerts.Add(new SystemAlert
                {
                    Type = "Warning",
                    Title = "High CPU Load",
                    Message = $"CPU utilization is at {snap.Cpu.OverallUsage:F1}%"
                });
            }

            if (snap.Memory.UsagePercentage > 90.0f)
            {
                alerts.Add(new SystemAlert
                {
                    Type = "Critical",
                    Title = "High Memory Usage",
                    Message = $"Physical Memory is at {snap.Memory.UsagePercentage:F1}% ({snap.Memory.UsedMb / 1024:F1} GB)"
                });
            }

            foreach (var d in snap.Disks)
            {
                if (d.UsagePercentage > 92.0f)
                {
                    alerts.Add(new SystemAlert
                    {
                        Type = "Warning",
                        Title = $"Drive Space Warning ({d.Name})",
                        Message = $"{d.VolumeLabel} has only {d.FreeGb:F1} GB ({100 - d.UsagePercentage:F1}%) remaining."
                    });
                }
            }

            return alerts;
        }

        public ProcessActionResult KillProcess(int pid)
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                string procName = proc.ProcessName;
                proc.Kill(entireProcessTree: true);
                return new ProcessActionResult
                {
                    Success = true,
                    Message = $"Successfully terminated process '{procName}' (PID: {pid})."
                };
            }
            catch (Exception ex)
            {
                return new ProcessActionResult
                {
                    Success = false,
                    Message = $"Failed to kill process {pid}: {ex.Message}"
                };
            }
        }

        public ProcessActionResult SetProcessPriority(int pid, string priorityName)
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                if (Enum.TryParse<ProcessPriorityClass>(priorityName, true, out var priorityClass))
                {
                    proc.PriorityClass = priorityClass;
                    return new ProcessActionResult
                    {
                        Success = true,
                        Message = $"Updated process '{proc.ProcessName}' (PID: {pid}) priority to {priorityClass}."
                    };
                }
                return new ProcessActionResult { Success = false, Message = $"Invalid priority level '{priorityName}'." };
            }
            catch (Exception ex)
            {
                return new ProcessActionResult
                {
                    Success = false,
                    Message = $"Failed to change priority for PID {pid}: {ex.Message}"
                };
            }
        }
    }
}
