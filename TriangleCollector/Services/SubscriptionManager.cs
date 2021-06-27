using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TriangleCollector.Models;
using TriangleCollector.Models.Interfaces;

namespace TriangleCollector.Services
{
    public class SubscriptionManager : BackgroundService
    {
        private readonly ILoggerFactory _factory;

        private readonly ILogger<SubscriptionManager> _logger;

        private IExchange Exchange;

        private decimal SubscriptionThreshold = -0.1m;

        public SubscriptionManager(ILoggerFactory factory, ILogger<SubscriptionManager> logger, IExchange exch)
        {
            _factory = factory;
            _logger = logger;
            Exchange = exch;
        }
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug($"Starting Subscription Manager for {Exchange.ExchangeName}...");

            stoppingToken.Register(() => _logger.LogDebug("Stopping Subscription Manager..."));
            BackgroundProcessing(stoppingToken);
            _logger.LogDebug("Stopped Subscription Manager.");
            return Task.CompletedTask;
        }
        private void BackgroundProcessing(CancellationToken stoppingToken)
        {
            var sw = new Stopwatch();
            sw.Start();
            Exchange.TradedMarkets = Exchange.ExchangeClient.GetMarketsViaRestApi(); 
            MarketMapper.MapOpportunities(Exchange);
            var queuedMarkets = new List<IOrderbook>();
            foreach(var market in Exchange.TradedMarkets)
            {
                if(!Exchange.SubscribedMarkets.Keys.Contains(market.Symbol))
                {
                    if(Exchange.TriarbMarketMapping.TryGetValue(market.Symbol, out var triangles))
                    {
                        foreach (var triangle in triangles)
                        {
                            CreateSnapshots.CreateOrderbookSnapshots(triangle);
                            triangle.SetMaxVolumeAndProfitability();
                            if (triangle.ProfitPercent > SubscriptionThreshold)
                            {
                                if (!Exchange.SubscribedMarkets.Keys.Contains(triangle.FirstSymbolOrderbook.Symbol) && !queuedMarkets.Contains(triangle.FirstSymbolOrderbook))
                                {
                                    Exchange.SubscriptionQueue.Enqueue(triangle.FirstSymbolOrderbook);
                                    queuedMarkets.Add(triangle.FirstSymbolOrderbook);
                                }
                                if (!Exchange.SubscribedMarkets.Keys.Contains(triangle.SecondSymbolOrderbook.Symbol) && !queuedMarkets.Contains(triangle.SecondSymbolOrderbook))
                                {
                                    Exchange.SubscriptionQueue.Enqueue(triangle.SecondSymbolOrderbook);
                                    queuedMarkets.Add(triangle.SecondSymbolOrderbook);
                                }
                                if (!Exchange.SubscribedMarkets.Keys.Contains(triangle.ThirdSymbolOrderbook.Symbol) && !queuedMarkets.Contains(triangle.ThirdSymbolOrderbook))
                                {
                                    Exchange.SubscriptionQueue.Enqueue(triangle.ThirdSymbolOrderbook);
                                    queuedMarkets.Add(triangle.ThirdSymbolOrderbook);
                                }
                            } 
                        }
                    }
                }
            }
            sw.Stop();
            //Console.WriteLine($"took {sw.ElapsedMilliseconds}ms to map markets");
        }
    }
}
