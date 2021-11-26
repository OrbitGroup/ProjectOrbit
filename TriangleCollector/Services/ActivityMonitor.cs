using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models.Interfaces;

namespace TriangleCollector.Services
{
    public class ActivityMonitor: BackgroundService
    {
        private readonly ILogger<ActivityMonitor> _logger;

        private readonly ILoggerFactory _factory;

        private readonly int LoopTimer = 5; //the interval (in seconds) for each printout of the activity monitor

        private IExchange Exchange { get; set; }

        private double LastOBCounter = 0;
        private double LastTriarbCounter = 0;

        public ActivityMonitor(ILoggerFactory factory, ILogger<ActivityMonitor> logger, IExchange exchange)
        {
            _logger = logger;
            _factory = factory;
            Exchange = exchange;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting Activity Monitor for {ExchangeName}...", Exchange.ExchangeName);

            stoppingToken.Register(() => _logger.LogDebug("Stopping Activity Monitor..."));
            await Task.Run(async () =>
            {
                await BackgroundProcessing(stoppingToken);
            }, stoppingToken);
        }

        public async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var task = Task.Run(async () =>
                {
                   await LogMetrics();
                }, stoppingToken);

                await Task.Delay(LoopTimer * 1000, stoppingToken);
            }
        }
        public Task LogMetrics()
        {
            var orderbookCount = Exchange.OrderbookUpdateStats.TryGetValue("Total Update Count", out int obCount);
            var activeClientCount = Exchange.ActiveClients.Count;
            var abortedClientCount = Exchange.InactiveClients.Count;
            double activeSubscriptions = Exchange.SubscribedMarkets.Count;
            double targetSubscriptions = Exchange.SubscribedMarkets.Count + Exchange.SubscriptionQueue.Count;
            double relevantRatio = Math.Round(targetSubscriptions / Exchange.TradedMarkets.Count, 2) * 100;

            double oldestClientAge = 0;
            if (Exchange.ActiveClients.Count > 0)
            {
                var oldestClient = Exchange.ActiveClients.OrderByDescending(c => c.TimeStarted).Last();
                oldestClientAge = Math.Round((DateTime.UtcNow - oldestClient.TimeStarted).TotalMinutes, 2);


                _logger.LogDebug("*********************************************************************************************************************************************" +
                    Environment.NewLine +
                    $"{Exchange.ExchangeName} --- Data Points Received: {obCount}. Data Receipts/Second (last {LoopTimer}s): {(obCount - LastOBCounter) / LoopTimer}." +
                    Environment.NewLine +
                    $"{Exchange.ExchangeName} --- Triarb Opportunities Calculated: {Exchange.TriangleCount}. Triarb Opportunities/ Second(last {LoopTimer}s): {(Exchange.TriangleCount - LastTriarbCounter) / LoopTimer}" +
                    Environment.NewLine +
                    $"{Exchange.ExchangeName} --- Queue Size: {Exchange.TrianglesToRecalculate.Count} - Active Subscriptions: {activeSubscriptions} - {Math.Round(activeSubscriptions / targetSubscriptions, 2) * 100}% subscribed. {relevantRatio}% of markets are deemed relevant." +
                    Environment.NewLine +
                    $"{Exchange.ExchangeName} --- Active Clients: {activeClientCount} - Aborted Clients: {abortedClientCount} - Oldest Active Client: {oldestClientAge} minutes" +
                    Environment.NewLine +
                    $"Notional USD Profit of all profitable triangles: ${Math.Round(Exchange.TotalUSDValueProfitableTriangles,2)} - USD Profit of triangles above 2% risk premium: ${Math.Round(Exchange.TotalUSDValueViableTriangles, 2)} - Estimated attainable profit: ${Math.Round(Exchange.EstimatedViableProfit,2)}" +
                    Environment.NewLine +
                    "*********************************************************************************************************************************************");

                LastOBCounter = obCount;
                LastTriarbCounter = Exchange.TriangleCount;
            }

            return Task.CompletedTask;
        }
    }
}
