using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SystemMonitor.Hubs;
using SystemMonitor.Models;
using SystemMonitor.Services;

#if WINDOWS
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
#endif

namespace SystemMonitor
{
    public static class Program
    {
#if WINDOWS
        public static MainWindow? MainWindowInstance { get; private set; }
#endif
        public static int BoundPort { get; private set; } = 5200;

        [STAThread]
        public static void Main(string[] args)
        {
            if (OperatingSystem.IsWindows())
            {
#if WINDOWS
                ApplicationConfiguration.Initialize();
                KillDuplicateInstances();
#endif
            }

            var baseDir = AppContext.BaseDirectory;
            var diskWebRoot = Path.Combine(baseDir, "wwwroot");
            if (!Directory.Exists(diskWebRoot))
            {
                Directory.CreateDirectory(diskWebRoot);
            }

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = baseDir,
                WebRootPath = diskWebRoot
            });

            builder.Services.AddSingleton<SystemMetricsCollector>();
            builder.Services.AddHostedService<MetricsBackgroundService>();
            builder.Services.AddSignalR();
            builder.Services.AddEndpointsApiExplorer();

            var webApp = builder.Build();

            webApp.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value?.ToLower() ?? "/";
                if (path == "/" || path == "/index.html")
                {
                    if (await TryServeFileOrResourceAsync(context, "index.html", "text/html")) return;
                }
                else if (path == "/css/styles.css")
                {
                    if (await TryServeFileOrResourceAsync(context, "css/styles.css", "text/css")) return;
                }
                else if (path == "/js/app.js")
                {
                    if (await TryServeFileOrResourceAsync(context, "js/app.js", "application/javascript")) return;
                }
                else if (path == "/js/signalr.min.js")
                {
                    if (await TryServeFileOrResourceAsync(context, "js/signalr.min.js", "application/javascript")) return;
                }

                await next();
            });

            webApp.UseDefaultFiles();
            webApp.UseStaticFiles();

            webApp.MapHub<MetricsHub>("/hubs/metrics");

            webApp.MapGet("/api/system/snapshot", (SystemMetricsCollector collector) =>
            {
                return Results.Ok(collector.CollectSnapshot());
            });

            webApp.MapPost("/api/memory/flush", (SystemMetricsCollector collector) =>
            {
                var res = collector.FlushMemory();
                return Results.Ok(res);
            });

            webApp.MapGet("/api/startup/programs", (SystemMetricsCollector collector) =>
            {
                return Results.Ok(collector.GetStartupPrograms());
            });

            webApp.MapPost("/api/benchmark/run", async (SystemMetricsCollector collector) =>
            {
                var res = await collector.RunCpuBenchmarkAsync(4);
                return Results.Ok(res);
            });

            webApp.MapPost("/api/window/mini", () =>
            {
#if WINDOWS
                MainWindowInstance?.SetMiniMode(true);
#endif
                return Results.Ok(new { success = true });
            });

            webApp.MapPost("/api/window/normal", () =>
            {
#if WINDOWS
                MainWindowInstance?.SetMiniMode(false);
#endif
                return Results.Ok(new { success = true });
            });

            webApp.MapDelete("/api/process/{pid:int}", (int pid, SystemMetricsCollector collector) =>
            {
                var result = collector.KillProcess(pid);
                return result.Success ? Results.Ok(result) : Results.BadRequest(result);
            });

            webApp.MapPost("/api/process/{pid:int}/priority", (int pid, PriorityRequest req, SystemMetricsCollector collector) =>
            {
                var result = collector.SetProcessPriority(pid, req.Priority);
                return result.Success ? Results.Ok(result) : Results.BadRequest(result);
            });

            webApp.MapGet("/api/system/export", (string? format, SystemMetricsCollector collector) =>
            {
                var snapshot = collector.CollectSnapshot();
                if (format?.ToLower() == "csv")
                {
                    var csv = new System.Text.StringBuilder();
                    csv.AppendLine("PID,ProcessName,MemoryMB,CpuPercentage,Threads,PriorityClass");
                    foreach (var p in snapshot.Processes)
                    {
                        csv.AppendLine($"{p.Pid},\"{p.Name}\",{p.WorkingSetMb},{p.CpuPercentage},{p.ThreadCount},{p.PriorityClass}");
                    }
                    return Results.File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"system_telemetry_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
                }

                var jsonStr = System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                return Results.File(System.Text.Encoding.UTF8.GetBytes(jsonStr), "application/json", $"system_telemetry_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
            });

            BoundPort = FindAvailablePort(5200, 5210);

            if (OperatingSystem.IsWindows())
            {
#if WINDOWS
                Task.Run(async () =>
                {
                    try
                    {
                        await webApp.RunAsync($"http://127.0.0.1:{BoundPort}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Kestrel Error]: {ex.Message}");
                    }
                });

                MainWindowInstance = new MainWindow(webApp);
                Application.Run(MainWindowInstance);
#endif
            }
            else
            {
                string targetUrl = $"http://127.0.0.1:{BoundPort}";
                Console.WriteLine($"=================================================");
                Console.WriteLine($" [SYSTEM PULSE v3.0] Linux Telemetry Host Active ");
                Console.WriteLine($" Dashboard URL: {targetUrl}");
                Console.WriteLine($"=================================================");

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "xdg-open",
                        Arguments = targetUrl,
                        UseShellExecute = true
                    });
                }
                catch { }

                webApp.Run(targetUrl);
            }
        }

        private static void KillDuplicateInstances()
        {
            try
            {
                int currentPid = Environment.ProcessId;
                foreach (var p in Process.GetProcessesByName("SystemMonitor"))
                {
                    if (p.Id != currentPid)
                    {
                        try { p.Kill(); } catch { }
                    }
                }
            }
            catch { }
        }

        private static int FindAvailablePort(int startPort, int endPort)
        {
            for (int port = startPort; port <= endPort; port++)
            {
                try
                {
                    var listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                    listener.Stop();
                    return port;
                }
                catch { }
            }
            return startPort;
        }

        private static async Task<bool> TryServeFileOrResourceAsync(HttpContext ctx, string relativePath, string contentType)
        {
            var assembly = typeof(Program).Assembly;
            var resourceName = $"SystemMonitor.wwwroot.{relativePath.Replace("/", ".").Replace("\\", ".")}";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                ctx.Response.ContentType = contentType;
                await stream.CopyToAsync(ctx.Response.Body);
                return true;
            }

            var diskPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", relativePath);
            if (File.Exists(diskPath))
            {
                ctx.Response.ContentType = contentType;
                await ctx.Response.SendFileAsync(diskPath);
                return true;
            }

            return false;
        }
    }

#if WINDOWS
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class MainWindow : Form
    {
        private readonly WebView2 _webView;
        private readonly WebApplication _webApp;
        private readonly NotifyIcon _trayIcon;
        private bool _allowExit = false;
        private bool _isMiniMode = false;

        public MainWindow(WebApplication webApp)
        {
            _webApp = webApp;

            Text = "SYSTEM PULSE - Real-Time C# System Monitor";
            Width = 1420;
            Height = 920;
            MinimumSize = new Size(340, 220);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(7, 10, 18);

            _webView = new WebView2
            {
                Dock = DockStyle.Fill
            };
            Controls.Add(_webView);

            var trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Open SYSTEM PULSE", null, (s, e) => RestoreWindow());
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("Exit", null, (s, e) => ForceExit());

            _trayIcon = new NotifyIcon
            {
                Text = "SYSTEM PULSE - Real-Time Monitor",
                Icon = SystemIcons.Application,
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            _trayIcon.DoubleClick += (s, e) => RestoreWindow();

            InitializeWebViewAsync();
            FormClosing += MainWindow_FormClosing;
        }

        public void SetMiniMode(bool enable)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetMiniMode(enable)));
                return;
            }

            _isMiniMode = enable;
            if (enable)
            {
                TopMost = true;
                Size = new Size(360, 240);
                FormBorderStyle = FormBorderStyle.SizableToolWindow;
            }
            else
            {
                TopMost = false;
                Size = new Size(1420, 920);
                FormBorderStyle = FormBorderStyle.Sizable;
                StartPosition = FormStartPosition.CenterScreen;
            }
        }

        private async void InitializeWebViewAsync()
        {
            try
            {
                await _webView.EnsureCoreWebView2Async();
                _webView.DefaultBackgroundColor = Color.FromArgb(7, 10, 18);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                string targetUrl = $"http://127.0.0.1:{Program.BoundPort}";

                using (var httpClient = new HttpClient())
                {
                    for (int i = 0; i < 40; i++)
                    {
                        try
                        {
                            var res = await httpClient.GetAsync($"{targetUrl}/api/system/snapshot");
                            if (res.IsSuccessStatusCode) break;
                        }
                        catch { }
                        await Task.Delay(150);
                    }
                }

                _webView.CoreWebView2.Navigate(targetUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", "WebView Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestoreWindow()
        {
            Show();
            SetMiniMode(false);
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void ForceExit()
        {
            _allowExit = true;
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            Close();
            Application.Exit();
        }

        private async void MainWindow_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!_allowExit)
            {
                e.Cancel = true;
                Hide();
                _trayIcon.ShowBalloonTip(2000, "SYSTEM PULSE", "App is running in the background system tray.", ToolTipIcon.Info);
                return;
            }

            try
            {
                await _webApp.StopAsync();
            }
            catch { }
        }
    }
#endif
}
