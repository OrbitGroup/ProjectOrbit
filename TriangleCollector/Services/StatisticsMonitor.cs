using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models;
using TriangleCollector.Models.Exchanges;
using TriangleCollector.Models.Interfaces;

namespace TriangleCollector.Services
{
    public class StatisticsMonitor : BackgroundService
    {
        private long AverageMergeTarget = 1;

        private double AverageOrderbookUpdateDeltaTarget = 1;

        private double AverageMerge = 0;

        private double MergeCount = 0;

        private double MergeTotal = 0; 

        private double AverageOrderbookUpdateDelta = 0;

        private double OrderbookUpdateCount = 0;

        private IExchange Exchange { get; set; }

        private double OrderbookUpdateTotal = 0;

        private readonly ILogger<StatisticsMonitor> _logger;

        public StatisticsMonitor(ILogger<StatisticsMonitor> logger, IExchange exch)
        {
            _logger = logger;
            Exchange = exch;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting Stats Monitor...");
            Task.Run(async () => await TriangleMetrics(stoppingToken));
            Task.Run(async () => await OrderbookUpdateMetrics(stoppingToken));

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

        private async Task TriangleMetrics(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (Exchange.RecalculatedTriangles.TryDequeue(out Triangle triangle))
                {
                    Exchange.TriangleCount++;
                    if (triangle.ProfitPercent > 0)
                    {
                        Exchange.TotalUSDValueProfitableTriangles += (triangle.Profit * USDMonitor.BTCUSDPrice);
                        if (triangle.ProfitPercent > 0.02m)
                        {
                            Exchange.TotalUSDValueViableTriangles += (triangle.Profit * USDMonitor.BTCUSDPrice);
                            Exchange.EstimatedViableProfit += (triangle.ProfitPercent - 0.02m) * triangle.MaxVolume;
                        }
                    }
                }
            }
        }

        private async Task OrderbookUpdateMetrics(CancellationToken stoppingToken)
        {
            Exchange.OrderbookUpdateStats["Total Update Count"] = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                if (Exchange.OrderbookUpdateQueue.TryDequeue(out var update))
                {
                    Exchange.OrderbookUpdateStats["Total Update Count"] += 1;
                    Exchange.OrderbookUpdateStats.AddOrUpdate(update.Item2,1, (key, oldvalue) => oldvalue +=1);
                }
            }
        }
    }
}
