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

            // 1. Build and start ASP.NET Core Web Server background host
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

            // 2. Create Native Windows Desktop Form with WebView2 embedded
            var mainForm = new MainWindow(webApp);
            Application.Run(mainForm);
        }
    }

    public class MainWindow : Form
    {
        private readonly WebView2 _webView;
        private readonly WebApplication _webApp;

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

            InitializeWebViewAsync();

            FormClosed += MainWindow_FormClosed;
        }

        private async void InitializeWebViewAsync()
        {
            try
            {
                await _webView.EnsureCoreWebView2Async();
                
                // Customize WebView background color to match dark glassmorphism
                _webView.DefaultBackgroundColor = Color.FromArgb(7, 10, 18);
                
                // Disable browser context menus for app-like native feel
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                // Navigate to local Kestrel server
                _webView.CoreWebView2.Navigate("http://localhost:5200");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize WebView2: {ex.Message}\nEnsure Microsoft Edge WebView2 runtime is installed.", 
                    "WebView Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void MainWindow_FormClosed(object? sender, FormClosedEventArgs e)
        {
            try
            {
                await _webApp.StopAsync();
            }
            catch { }
            Application.Exit();
        }
    }
}
