using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;

namespace TriangleCollector.Services
{
    public class QueueMonitor : BackgroundService
    {
        private int QueueSizeTarget = 10;

        private readonly ILoggerFactory _factory;

        private readonly ILogger<QueueMonitor> _logger;

        private int calculatorCount = 1;

        private int MaxTriangleCalculatorQueueLength = 1000;

        private int MaxTriangleCalculators = 7;

        private int NumberOfSecondsUntilStale = 60;

        public QueueMonitor(ILoggerFactory factory, ILogger<QueueMonitor> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting Queue Monitor...");

            stoppingToken.Register(() => _logger.LogDebug("Stopping Queue Monitor..."));
            await BackgroundProcessing(stoppingToken);
            _logger.LogDebug("Stopped Queue Monitor.");
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (TriangleCollector.Clients.Any(x => x.State != WebSocketState.Open && x.State != WebSocketState.Connecting))
                {
                    _logger.LogError("One or more clients have disconnected");
                }

                if (TriangleCollector.OfficialOrderbooks.Count > 0 && TriangleCollector.TrianglesToRecalculate.Count > QueueSizeTarget)
                {
                    _logger.LogWarning($"Orderbooks: {TriangleCollector.OfficialOrderbooks.Count} - Triangles: {TriangleCollector.Triangles.Count} - TrianglesToRecalc: {TriangleCollector.TrianglesToRecalculate.Count}");
                }

                var sb = new StringBuilder();

                int count = 0;
                foreach (var triangle in TriangleCollector.Triangles.Values.OrderByDescending(x => x.ProfitPercent))
                {
                    if (true)
                    {
                        TriangleCollector.TriangleRefreshTimes.TryGetValue(triangle.ToString(), out DateTime refreshTime);
                        var delay = DateTime.UtcNow.Subtract(refreshTime).TotalSeconds;

                        //TriangleCollector.Triangles.TryGetValue(triangle.Key, out )

                        sb.Append($"Triangle: {triangle} | Profit: {triangle.ProfitPercent} | Volume: {triangle.MaxVolume} | Last Updated: {refreshTime} | Delay: {delay} seconds\n");
                        count++;
                    }
                    
                    if (count == 5)
                    {
                        break;
                    }
                }

                if (TriangleCollector.Triangles.Count > 0)
                {
                    _logger.LogDebug($"{sb}");
                }

                if (TriangleCollector.TrianglesToRecalculate.Count > MaxTriangleCalculatorQueueLength && calculatorCount < MaxTriangleCalculators)
                {
                    //TODO: implement average queue size metric to decrement TriangleCalculators.
                    calculatorCount++;
                    var newCalc = new TriangleCalculator(_factory.CreateLogger<TriangleCalculator>(), calculatorCount);
                    await newCalc.StartAsync(stoppingToken);
                }
                
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
