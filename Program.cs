using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SystemMonitor.Hubs;
using SystemMonitor.Services;
using SystemMonitor.Models;

var builder = WebApplication.CreateBuilder(args);

// Register telemetry services
builder.Services.AddSingleton<SystemMetricsCollector>();
builder.Services.AddHostedService<MetricsBackgroundService>();

// SignalR for low-latency live web dashboard streaming
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// SignalR Real-Time Telemetry Endpoint
app.MapHub<MetricsHub>("/hubs/metrics");

// REST API Endpoints
app.MapGet("/api/system/snapshot", (SystemMetricsCollector collector) =>
{
    return Results.Ok(collector.CollectSnapshot());
});

app.MapDelete("/api/process/{pid:int}", (int pid, SystemMetricsCollector collector) =>
{
    var result = collector.KillProcess(pid);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
});

// Automatically launch browser when SystemMonitor starts
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "http://localhost:5200",
            UseShellExecute = true
        });
    }
    catch { }
});

app.Run("http://localhost:5200");
