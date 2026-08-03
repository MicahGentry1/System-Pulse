using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using SystemMonitor.Hubs;

namespace SystemMonitor.Services
{
    public class MetricsBackgroundService : BackgroundService
    {
        private readonly SystemMetricsCollector _collector;
        private readonly IHubContext<MetricsHub> _hubContext;
        private readonly ILogger<MetricsBackgroundService> _logger;

        public MetricsBackgroundService(
            SystemMetricsCollector collector,
            IHubContext<MetricsHub> hubContext,
            ILogger<MetricsBackgroundService> logger)
        {
            _collector = collector;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("System Telemetry Service started.");

            // Warm up counters
            try
            {
                _collector.CollectSnapshot();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Warmup error during snapshot collection");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var snapshot = _collector.CollectSnapshot();
                    await _hubContext.Clients.All.SendAsync("ReceiveMetrics", snapshot, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error pushing system telemetry via SignalR.");
                }

                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
