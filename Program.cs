using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Web.WebView2.WinForms;
using SystemMonitor.Hubs;
using SystemMonitor.Models;
using SystemMonitor.Services;

namespace SystemMonitor
{
    public static class Program
    {
        public static MainWindow? MainWindowInstance { get; private set; }

        [STAThread]
        public static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();

            var baseDir = AppContext.BaseDirectory;
            var diskWebRoot = Path.Combine(baseDir, "wwwroot");

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = baseDir,
                WebRootPath = Directory.Exists(diskWebRoot) ? diskWebRoot : baseDir
            });

            builder.Services.AddSingleton<SystemMetricsCollector>();
            builder.Services.AddHostedService<MetricsBackgroundService>();
            builder.Services.AddSignalR();
            builder.Services.AddEndpointsApiExplorer();

            var webApp = builder.Build();

            // Configure EmbeddedFileProvider for static web assets
            var assembly = typeof(Program).Assembly;
            IFileProvider fileProvider;
            try
            {
                var embeddedProvider = new EmbeddedFileProvider(assembly, "SystemMonitor.wwwroot");
                if (Directory.Exists(diskWebRoot))
                {
                    fileProvider = new CompositeFileProvider(new PhysicalFileProvider(diskWebRoot), embeddedProvider);
                }
                else
                {
                    fileProvider = embeddedProvider;
                }
            }
            catch
            {
                fileProvider = new PhysicalFileProvider(Directory.Exists(diskWebRoot) ? diskWebRoot : baseDir);
            }

            webApp.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
            webApp.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });

            // Fail-safe embedded resource routes for single-file deployment
            webApp.MapGet("/", async (HttpContext ctx) => await ServeEmbeddedAssetAsync(ctx, "index.html", "text/html"));
            webApp.MapGet("/index.html", async (HttpContext ctx) => await ServeEmbeddedAssetAsync(ctx, "index.html", "text/html"));
            webApp.MapGet("/css/styles.css", async (HttpContext ctx) => await ServeEmbeddedAssetAsync(ctx, "css.styles.css", "text/css"));
            webApp.MapGet("/js/app.js", async (HttpContext ctx) => await ServeEmbeddedAssetAsync(ctx, "js.app.js", "application/javascript"));

            webApp.MapHub<MetricsHub>("/hubs/metrics");

            webApp.MapGet("/api/system/snapshot", (SystemMetricsCollector collector) =>
            {
                return Results.Ok(collector.CollectSnapshot());
            });

            webApp.MapPost("/api/benchmark/run", async (SystemMetricsCollector collector) =>
            {
                var res = await collector.RunCpuBenchmarkAsync(4);
                return Results.Ok(res);
            });

            webApp.MapPost("/api/window/mini", () =>
            {
                MainWindowInstance?.SetMiniMode(true);
                return Results.Ok(new { success = true });
            });

            webApp.MapPost("/api/window/normal", () =>
            {
                MainWindowInstance?.SetMiniMode(false);
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

            Task.Run(async () =>
            {
                try
                {
                    await webApp.RunAsync("http://localhost:5200");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Kestrel Error]: {ex.Message}");
                }
            });

            MainWindowInstance = new MainWindow(webApp);
            Application.Run(MainWindowInstance);
        }

        private static async Task ServeEmbeddedAssetAsync(HttpContext ctx, string relativePath, string contentType)
        {
            var assembly = typeof(Program).Assembly;
            var resourceName = $"SystemMonitor.wwwroot.{relativePath}";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                ctx.Response.ContentType = contentType;
                await stream.CopyToAsync(ctx.Response.Body);
                return;
            }

            // Fallback to disk if resource stream name varies
            var diskPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", relativePath.Replace(".", "/"));
            if (File.Exists(diskPath))
            {
                ctx.Response.ContentType = contentType;
                await ctx.Response.SendFileAsync(diskPath);
            }
        }
    }

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

                using (var httpClient = new HttpClient())
                {
                    for (int i = 0; i < 35; i++)
                    {
                        try
                        {
                            var res = await httpClient.GetAsync("http://localhost:5200/api/system/snapshot");
                            if (res.IsSuccessStatusCode) break;
                        }
                        catch { }
                        await Task.Delay(200);
                    }
                }

                _webView.CoreWebView2.Navigate("http://localhost:5200");
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
}
