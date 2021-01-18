using System;
using System.Collections.Generic;
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
            while (!stoppingToken.IsCancellationRequested)
            {
                Exchange.TradedMarkets = Exchange.ExchangeClient.GetMarketsViaRestApi(); 
                MarketMapper.MapOpportunities(Exchange);
                foreach(var market in Exchange.TradedMarkets)
                {
                    if(!Exchange.SubscribedMarkets.Contains(market) && !Exchange.SubscriptionQueue.Contains(market))
                    {
                        if(Exchange.TriarbMarketMapping.TryGetValue(market.Symbol, out var triangles))
                        {
                            foreach (var triangle in triangles)
                            {
                                Exchange.OfficialOrderbooks.TryGetValue(triangle.FirstSymbol, out IOrderbook firstSymbolOrderbook);
                                Exchange.OfficialOrderbooks.TryGetValue(triangle.SecondSymbol, out IOrderbook secondSymbolOrderbook);
                                Exchange.OfficialOrderbooks.TryGetValue(triangle.ThirdSymbol, out IOrderbook thirdSymbolOrderbook);
                                triangle.SetMaxVolumeAndProfitability(firstSymbolOrderbook, secondSymbolOrderbook, thirdSymbolOrderbook);

                                if (triangle.ProfitPercent > SubscriptionThreshold)
                                {
                                    if (!Exchange.SubscribedMarkets.Contains(triangle.FirstSymbolOrderbook) && !Exchange.SubscriptionQueue.Contains(triangle.FirstSymbolOrderbook))
                                    {
                                        triangle.FirstSymbolOrderbook.OfficialAsks.Clear();
                                        triangle.FirstSymbolOrderbook.OfficialBids.Clear();
                                        Exchange.SubscriptionQueue.Enqueue(triangle.FirstSymbolOrderbook);
                                    }
                                    if (!Exchange.SubscribedMarkets.Contains(triangle.SecondSymbolOrderbook) && !Exchange.SubscriptionQueue.Contains(triangle.SecondSymbolOrderbook))
                                    {
                                        triangle.SecondSymbolOrderbook.OfficialAsks.Clear();
                                        triangle.SecondSymbolOrderbook.OfficialBids.Clear();
                                        Exchange.SubscriptionQueue.Enqueue(triangle.SecondSymbolOrderbook);
                                    }
                                    if (!Exchange.SubscribedMarkets.Contains(triangle.ThirdSymbolOrderbook) && !Exchange.SubscriptionQueue.Contains(triangle.ThirdSymbolOrderbook))
                                    {
                                        triangle.ThirdSymbolOrderbook.OfficialAsks.Clear();
                                        triangle.ThirdSymbolOrderbook.OfficialBids.Clear();
                                        Exchange.SubscriptionQueue.Enqueue(triangle.ThirdSymbolOrderbook);
                                    }
                                }
                            }
                        }
                    }
                }
                await Task.Delay(TimeInterval * 60 * 1000);
            }
        }
    }
}
