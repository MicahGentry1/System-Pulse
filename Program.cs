using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Web.WebView2.WinForms;
using SystemMonitor.Hubs;
using SystemMonitor.Models;
using SystemMonitor.Services;

namespace SystemMonitor
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();

            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddSingleton<SystemMetricsCollector>();
            builder.Services.AddHostedService<MetricsBackgroundService>();
            builder.Services.AddSignalR();
            builder.Services.AddEndpointsApiExplorer();

            var webApp = builder.Build();

            webApp.UseDefaultFiles();
            webApp.UseStaticFiles();

            webApp.MapHub<MetricsHub>("/hubs/metrics");

            webApp.MapGet("/api/system/snapshot", (SystemMetricsCollector collector) =>
            {
                return Results.Ok(collector.CollectSnapshot());
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

            // Start Kestrel web server on http://localhost:5200 asynchronously
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

            var mainForm = new MainWindow(webApp);
            Application.Run(mainForm);
        }
    }

    public class MainWindow : Form
    {
        private readonly WebView2 _webView;
        private readonly WebApplication _webApp;
        private readonly NotifyIcon _trayIcon;
        private bool _allowExit = false;

        public MainWindow(WebApplication webApp)
        {
            _webApp = webApp;

            Text = "SYSTEM PULSE - Real-Time C# System Monitor";
            Width = 1420;
            Height = 920;
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(7, 10, 18);

            _webView = new WebView2
            {
                Dock = DockStyle.Fill
            };
            Controls.Add(_webView);

            // Create System Tray Icon & Context Menu
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

        private async void InitializeWebViewAsync()
        {
            try
            {
                await _webView.EnsureCoreWebView2Async();
                _webView.DefaultBackgroundColor = Color.FromArgb(7, 10, 18);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
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
