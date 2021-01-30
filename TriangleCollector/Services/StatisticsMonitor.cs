using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TriangleCollector.Services
{
    public class StatisticsMonitor : BackgroundService
    {
        private long AverageMergeTarget = 1;

        private double AverageOrderbookUpdateDeltaTarget = 1;

        private double AverageMerge = 0;

        private double MergeCount = 0;

        private double AverageOrderbookUpdateDelta = 0;

        private double OrderbookUpdateCount = 0;

        private readonly ILogger<StatisticsMonitor> _logger;

        public StatisticsMonitor(ILogger<StatisticsMonitor> logger)
        {
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting Stats Monitor...");
            //Task.Run(async () => await MergeProcessing(stoppingToken));
            //Task.Run(async () => await OrderbookUpdateProcessing(stoppingToken));

            while (!stoppingToken.IsCancellationRequested)
            {
                if (MergeCount > 0 && OrderbookUpdateCount > 0)
                {
                    if (AverageMerge > AverageMergeTarget || AverageOrderbookUpdateDelta > AverageOrderbookUpdateDeltaTarget)
                    {
                        _logger.LogWarning($"Average Merge: {AverageMerge} ms | Average Update Delta: {AverageOrderbookUpdateDelta} seconds");
                    }
                }
                await Task.Delay(10000);
            }
        }

        private async Task MergeProcessing(CancellationToken stoppingToken)
        {
            /*while (!stoppingToken.IsCancellationRequested)
            {
                if (TriangleCollector.MergeTimings.TryDequeue(out long merge))
                {
                    MergeCount++;
                    MergeTotal += merge;
                    AverageMerge = MergeTotal / MergeCount;
                }
            }*/
        }

        private async Task OrderbookUpdateProcessing(CancellationToken stoppingToken)
        {
            /*while (!stoppingToken.IsCancellationRequested)
            {
                if (TriangleCollector.OrderbookUpdateDeltas.TryDequeue(out TimeSpan delta))
                {
                    OrderbookUpdateCount++;
                    OrderbookUpdateTotal += delta.TotalSeconds;
                    AverageOrderbookUpdateDelta = OrderbookUpdateTotal / OrderbookUpdateCount;
                }
            }*/
        }
    }
}
