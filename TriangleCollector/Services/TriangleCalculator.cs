﻿using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models;
using TriangleCollector.Models.Interfaces;

namespace TriangleCollector.Services
{
    public class TriangleCalculator : BackgroundService
    {
        private readonly ILogger<TriangleCalculator> _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly int CalculatorId;

        private IExchange Exchange { get; set; }

        public TriangleCalculator(ILogger<TriangleCalculator> logger, TelemetryClient telemetryClient, IExchange exch)
        {
            _logger = logger;
            _telemetryClient = telemetryClient;
            CalculatorId = 1;
            Exchange = exch;
        }

        public TriangleCalculator(ILogger<TriangleCalculator> logger, TelemetryClient telemetryClient, int calculatorCount, IExchange exch)
        {
            _logger = logger;
            _telemetryClient = telemetryClient;
            CalculatorId = calculatorCount;
            Exchange = exch;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() => _logger.LogDebug("Stopping Triangle Calculator..."));

            _logger.LogDebug($"Starting Triangle Calculator {CalculatorId} for {Exchange.ExchangeName}");

            await Task.Run(async () =>
            {
                await BackgroundProcessing(stoppingToken);
            }, stoppingToken);

            _logger.LogDebug("Stopped Triangle Calculator.");
        }

        private Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            var triangleCalculationMetric = _telemetryClient.GetMetric("TriangleCalculation");

            while (!stoppingToken.IsCancellationRequested)
            {
                var sw = new Stopwatch();
                if (Exchange.TrianglesToRecalculate.TryDequeue(out Triangle triangle))
                {
                    sw.Start();
                    triangle.SetMaxVolumeAndProfitability();
                    sw.Stop();

                    if (sw.ElapsedMilliseconds > 50)
                    {
                        _logger.LogWarning($"Irregular triangle calculation time for {triangle}: {sw.ElapsedMilliseconds}ms in grand total" + Environment.NewLine +
                            $"Profitability calculation time, count: {triangle.ProfitabilityComputeTime}ms, {triangle.ProfitabilityComputeCount}, Volume calculation time: {triangle.VolumeComputeTime}ms, Liquidity removal time: {triangle.LiquidityRemovalComputeTime}ms" + Environment.NewLine + 
                            $"OB 1 size: {triangle.FirstOrderBook.Count}, OB 2 size: {triangle.SecondOrderBook.Count}, OB 3 size {triangle.ThirdOrderBook.Count}");
                    }
                    sw.Reset();

                    triangleCalculationMetric.TrackValue(1);

                    var properties = new Dictionary<string, string>
                        {
                            { "ExchangeName", Exchange.ExchangeName },
                            { "TriangleId", triangle.ToString() },
                            { "ProfitPercent", triangle.ProfitPercent.ToString() },
                            { "MaxVolume", triangle.MaxVolume.ToString() },
                            { "BTCUSDPrice", USDMonitor.BTCUSDPrice.ToString() }
                        };

                    //TODO: Organize the min-profit percent and min-volume as part of IExchange
                    if (triangle.ProfitPercent > 0.05m)
                    {
                        //Check if the triangle has been recently traded. This is determined by the cache's AbsoluteExpirationRelativeToNow property which is set in the Trader.
                        if (!Exchange.RecentlyTradedTriangles.TryGetValue(triangle.ToString(), out _))
                        {
                            //Triangle isn't cached, so we write it to the TradeQueue channel and log the queued trade.
                            Exchange.TradeQueue.Writer.TryWrite(triangle);
                            _telemetryClient.TrackEvent("QueuedTrade", properties);
                        }
                    }
                    else
                    {
                        //In the current FullMode of the Channel<Triangle> this will never happen
                        //because we are dropping the oldest item in the queue and will always successfully write a triangle.
                        _logger.LogTrace("Failed to queue Triangle", properties);
                    }

                    if (triangle.ProfitPercent > 0)
                    {
                        //Logging all triangles over 0% profit just for data.
                        _telemetryClient.TrackEvent("ProfitableTriangle", properties);
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}
