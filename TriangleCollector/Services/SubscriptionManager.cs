using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TriangleCollector.Models.Interfaces;

namespace TriangleCollector.Services
{
    public class SubscriptionManager : BackgroundService
    {
        private readonly ILoggerFactory _factory;

        private readonly ILogger<SubscriptionManager> _logger;

        private IExchange Exchange;

        private int TimeInterval = 1; //time interval in minutes

        private decimal SubscriptionThreshold = -0.03m;

        public SubscriptionManager(ILoggerFactory factory, ILogger<SubscriptionManager> logger, IExchange exch)
        {
            _factory = factory;
            _logger = logger;
            Exchange = exch;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug($"Starting Subscription Manager for {Exchange.ExchangeName}...");

            stoppingToken.Register(() => _logger.LogDebug("Stopping Subscription Manager..."));
            await BackgroundProcessing(stoppingToken);
            _logger.LogDebug("Stopped Subscription Manager.");
        }
        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            var sw = new Stopwatch();
            while (!stoppingToken.IsCancellationRequested)
            {
                sw.Start();
                Exchange.TradedMarkets = Exchange.ExchangeClient.GetMarketsViaRestApi(); 
                MarketMapper.MapOpportunities(Exchange);
                foreach(var market in Exchange.TradedMarkets)
                {
                    if(!Exchange.SubscribedMarkets.Keys.Contains(market.Symbol))
                    {
                        if(Exchange.TriarbMarketMapping.TryGetValue(market.Symbol, out var triangles))
                        {
                            foreach (var triangle in triangles)
                            {
                                Exchange.OfficialOrderbooks.TryGetValue(triangle.FirstSymbol, out IOrderbook firstSymbolOrderbook);
                                Exchange.OfficialOrderbooks.TryGetValue(triangle.SecondSymbol, out IOrderbook secondSymbolOrderbook);
                                Exchange.OfficialOrderbooks.TryGetValue(triangle.ThirdSymbol, out IOrderbook thirdSymbolOrderbook);
                                if(firstSymbolOrderbook.OfficialAsks.Count>0 && firstSymbolOrderbook.OfficialBids.Count>0 
                                    && secondSymbolOrderbook.OfficialAsks.Count>0 && secondSymbolOrderbook.OfficialBids.Count>0 
                                    && thirdSymbolOrderbook.OfficialAsks.Count>0 && thirdSymbolOrderbook.OfficialBids.Count>0)
                                {
                                    triangle.SetMaxVolumeAndProfitability(firstSymbolOrderbook, secondSymbolOrderbook, thirdSymbolOrderbook);
                                    if (triangle.ProfitPercent > SubscriptionThreshold)
                                    {
                                        if (!Exchange.SubscribedMarkets.Keys.Contains(triangle.FirstSymbolOrderbook.Symbol) && !Exchange.SubscriptionQueue.Contains(triangle.FirstSymbolOrderbook))
                                        {
                                            Exchange.SubscriptionQueue.Enqueue(triangle.FirstSymbolOrderbook);
                                        }
                                        if (!Exchange.SubscribedMarkets.Keys.Contains(triangle.SecondSymbolOrderbook.Symbol) && !Exchange.SubscriptionQueue.Contains(triangle.SecondSymbolOrderbook))
                                        {
                                            Exchange.SubscriptionQueue.Enqueue(triangle.SecondSymbolOrderbook);
                                        }
                                        if (!Exchange.SubscribedMarkets.Keys.Contains(triangle.ThirdSymbolOrderbook.Symbol) && !Exchange.SubscriptionQueue.Contains(triangle.ThirdSymbolOrderbook))
                                        {
                                            Exchange.SubscriptionQueue.Enqueue(triangle.ThirdSymbolOrderbook);
                                        }
                                    }
                                } 
                            }
                        }
                    }
                }
                sw.Stop();
                //Console.WriteLine($"took {sw.ElapsedMilliseconds}ms to map markets");
                await Task.Delay(TimeInterval * 60 * 10000000);
            }
        }
    }
}
