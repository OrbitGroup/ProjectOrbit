using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models;
using TriangleCollector.Models.Interfaces;

namespace TriangleCollector.Services
{
    public class Trader : BackgroundService
    {
        private readonly ILogger<Trader> _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly IExchange Exchange;

        private readonly int MaxOrderBookAge = 30;
        private readonly string MaxOrderbookAgeUnits = "s";
        private readonly decimal MinVolume = 0.002m; //TODO: needs to go inside the Exchange, I just made this number up
        private readonly decimal MinUsdProfit = 25;
        private readonly int TriangleCooldownPeriod = 30;

        private Metric RulesEvaluationMetric;

        public Trader(ILogger<Trader> logger, TelemetryClient telemetryClient, IExchange exch)
        {
            _logger = logger;
            _telemetryClient = telemetryClient;
            Exchange = exch;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() => _logger.LogDebug("Stopping Trader..."));

            _logger.LogDebug($"Starting Trader for {Exchange.ExchangeName}");

            await Task.Run(async () =>
            {
                await BackgroundProcessing(stoppingToken);
            }, stoppingToken);

            _logger.LogDebug("Stopped Triangle Calculator.");
        }

        private async Task BackgroundProcessing(CancellationToken cancellationToken)
        {
            RulesEvaluationMetric = _telemetryClient.GetMetric("RulesEvaluation", "Category");

            //This is equivalent to while(true) read from the queue, or Channel in this case.
            await foreach (Triangle triangle in Exchange.TradeQueue.Reader.ReadAllAsync(cancellationToken))
            {
                //Double checking that the value isn't cached. This might happen if by chance two TriangleCalculators both added a triangle to the queue at the same time.
                if (!Exchange.RecentlyTradedTriangles.TryGetValue(triangle.ToString(), out _) && RulesEvaluation(triangle, out var category))
                {
                    //Add the triangle to the queue so triangle calculators don't waste space in the queue with something we already know is a triangle. 
                    //This timespan should be set to roughly how long it takes to complete the arbitrage, maybe plus some buffer.
                    Exchange.RecentlyTradedTriangles.Set(triangle.ToString(), DateTime.UtcNow, TimeSpan.FromSeconds(TriangleCooldownPeriod));

                    //This is where we would call our trade methods.
                    var usdProfit = $"{ USDMonitor.BTCUSDPrice * triangle.MaxVolume * triangle.ProfitPercent}";
                    Console.WriteLine($"{DateTime.Now}: {triangle} => ${usdProfit}");

                    var properties = new Dictionary<string, string>
                    {
                        { "ExchangeName", Exchange.ExchangeName },
                        { "Triangle", triangle.ToString()},
                        { "USDProfit", usdProfit },
                        { "ProfitPercent", triangle.ProfitPercent.ToString() },
                        { "MaxVolume", triangle.MaxVolume.ToString() },
                        { "MaxOrderbookAge", $"{MaxOrderBookAge}{MaxOrderbookAgeUnits}" },
                        { "TriangleCooldownPeriod", TriangleCooldownPeriod.ToString() },
                        { "FirstTradeResult", "SuccessOrFail" },
                        { "SecondTradeResult", "SuccessOrFail" },
                        { "ThirdTradeResult", "SuccessOrFail" }
                    };
                    _telemetryClient.TrackEvent("TradeAttempt", properties);
                }
                else
                {
                    //Rules evaluation failed, wait a smaller amount of time to see if the coin becomes feasible.
                    Exchange.RecentlyTradedTriangles.Set(triangle.ToString(), DateTime.UtcNow, TimeSpan.FromSeconds(5));
                }
            }
        }

        //TODO: implement rules engine, either from scratch or something like NRules
        private bool RulesEvaluation(Triangle triangle, out string category)
        {
            category = "";

            //When the app first starts we don't have the BTCUSD price, this stops us from logging a supposedly $0 trade.
            if (USDMonitor.BTCUSDPrice == 0) category += "|BTCUSD Price Not Populated";

            //Orderbooks appear to be able to get as old as 30 seconds. This TimeSpan value could be used as another way of adjusting our risk tolerance besides the ProfitPercent.
            //i.e. The higher the ProfitPercent the higher this TimeSpan could go.
            if (DateTime.UtcNow - triangle.FirstSymbolOrderbook.Timestamp >= TimeSpan.FromSeconds(MaxOrderBookAge)) category += "|First Orderbook Stale";
            if (DateTime.UtcNow - triangle.SecondSymbolOrderbook.Timestamp >= TimeSpan.FromSeconds(MaxOrderBookAge)) category += "|Second Orderbook Stale";
            if (DateTime.UtcNow - triangle.ThirdSymbolOrderbook.Timestamp >= TimeSpan.FromSeconds(MaxOrderBookAge)) category += "|Third Orderbook Stale";

            if (triangle.Profit >= MinUsdProfit) category += "|Low Volume";

            if (string.IsNullOrEmpty(category))
            {
                category += "Passed";
                //Console.WriteLine(category);
                //Console.WriteLine($"{triangle}: {DateTime.UtcNow - triangle.FirstSymbolOrderbook.Timestamp}, {DateTime.UtcNow - triangle.SecondSymbolOrderbook.Timestamp}, {DateTime.UtcNow - triangle.ThirdSymbolOrderbook.Timestamp}");
                RulesEvaluationMetric.TrackValue(1, category);
                return true;
            }

            //if (category.Contains("Stale"))
            //{
            //    Console.WriteLine(category);
            //    Console.WriteLine($"{triangle}: {DateTime.UtcNow - triangle.FirstSymbolOrderbook.Timestamp}, {DateTime.UtcNow - triangle.SecondSymbolOrderbook.Timestamp}, {DateTime.UtcNow - triangle.ThirdSymbolOrderbook.Timestamp}");
            //}

            RulesEvaluationMetric.TrackValue(1, category);
            return false;
        }
    }
}
