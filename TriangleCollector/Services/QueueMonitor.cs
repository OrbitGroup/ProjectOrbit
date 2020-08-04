using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector;

namespace TriangleCollector.Services
{
    public class QueueMonitor : BackgroundService
    {
        private ILoggerFactory _factory;

        private readonly ILogger<QueueMonitor> _logger;

        private int calculatorCount = 1;

        private int MaxTriangleCalculatorQueueLength = 100;

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
                if (TriangleCollector.OfficialOrderbooks.Count > 0)
                {
                    _logger.LogDebug($"\nOrderbooks: {TriangleCollector.OfficialOrderbooks.Count}\nTriangles: {TriangleCollector.Triangles.Count}\nUpdatedSymbols: {TriangleCollector.UpdatedSymbols.Count}\nTrianglesToRecalc: {TriangleCollector.TrianglesToRecalculate.Count}\n");
                }

                var sb = new StringBuilder();
                int count = 0;
                foreach (var triangle in TriangleCollector.Triangles.OrderByDescending(x => x.Value))
                {
                    if (triangle.Value > 0)
                    {
                        sb.Append($"\n{triangle.Key} : {triangle.Value}");
                        count++;
                    }
                    if (count == 5)
                    {
                        break;
                    }
                }

                if (TriangleCollector.Triangles.Count > 0)
                {
                    _logger.LogDebug($"{sb}\n");
                }

                if (TriangleCollector.TrianglesToRecalculate.Count > MaxTriangleCalculatorQueueLength)
                {
                    calculatorCount++;
                    var newCalc = new TriangleCalculator(_factory.CreateLogger<TriangleCalculator>(), calculatorCount);
                    newCalc.StartAsync(stoppingToken);
                }

                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}
