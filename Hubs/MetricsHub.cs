using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using SystemMonitor.Models;
using SystemMonitor.Services;

namespace SystemMonitor.Hubs
{
    public class MetricsHub : Hub
    {
        private readonly SystemMetricsCollector _collector;

        public MetricsHub(SystemMetricsCollector collector)
        {
            _collector = collector;
        }

        public async Task RequestMetrics()
        {
            var snapshot = _collector.CollectSnapshot();
            await Clients.Caller.SendAsync("ReceiveMetrics", snapshot);
        }

        public Task<ProcessKillResult> KillProcess(int pid)
        {
            var result = _collector.KillProcess(pid);
            return Task.FromResult(result);
        }
    }
}
