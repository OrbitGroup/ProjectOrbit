using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models.Interfaces;

namespace TriangleCollector.Services
{
    public class OrderbookSubscriber : BackgroundService
    {
        private readonly ILogger<OrderbookSubscriber> _logger;

        private readonly ILoggerFactory _factory;

        public OrderbookSubscriber(ILoggerFactory factory, ILogger<OrderbookSubscriber> logger)
        {
            _logger = logger;
            _factory = factory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting Orderbook Subscriber...");

            stoppingToken.Register(() => _logger.LogDebug("Stopping Orderbook Subscriber..."));
            await Task.Run(async () =>
            {
                BackgroundProcessing(stoppingToken);
            }, stoppingToken);
        }

        public void BackgroundProcessing(CancellationToken stoppingToken)
        {
            Parallel.ForEach(TriangleCollector.Exchanges, async (exchange) =>
            {
                string exchangeName = exchange.ExchangeName;
                //_logger.LogDebug($"{exchange.exchangeName}: Subscribing to {exchange.triarbEligibleMarkets.Count()} markets.");

                //we also start a TriangleCalculator for each exchange here so that we are ready to dequeue and calculate triangles as soon as the subscriptions are intialized.
                var calculator = new TriangleCalculator(_factory.CreateLogger<TriangleCalculator>(), exchange);
                await calculator.StartAsync(stoppingToken);

                //also a QueueMonitor for each exchange

                var monitor = new QueueMonitor(_factory, _factory.CreateLogger<QueueMonitor>(), exchange);
                await monitor.StartAsync(stoppingToken);

                var subscribedMarkets = 0;
                var eligibleMarkets = exchange.TriarbEligibleMarkets.ToList();
                try
                {
                    var sw = new Stopwatch();
                    while (subscribedMarkets < eligibleMarkets.Count)
                    {
                        _logger.LogInformation($"Starting subscribing to {exchange.ExchangeClient.MaxMarketsPerClient} markets on {exchange.ExchangeName}");
                        sw.Reset();
                        sw.Start();
                        var client = await exchange.ExchangeClient.GetExchangeClientAsync();
                        var listener = new OrderbookListener(_factory.CreateLogger<OrderbookListener>(), client, exchange);
                        await listener.StartAsync(stoppingToken);
                        var markets = eligibleMarkets.Skip(subscribedMarkets).Take(exchange.ExchangeClient.MaxMarketsPerClient).ToList();
                        //var markets = eligibleMarkets.GetRange(subscribedMarkets, exchange.ExchangeClient.MaxMarketsPerClient);
                        await exchange.ExchangeClient.Subscribe(markets);
                        
                        subscribedMarkets += exchange.ExchangeClient.MaxMarketsPerClient;
                        sw.Stop();
                        _logger.LogInformation($"Took {sw.ElapsedMilliseconds}ms to subscribe on {exchange.ExchangeName}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"issue with subscribing on {exchange.ExchangeName}");
                    _logger.LogError(ex.Message);
                    throw ex;
                }
                _logger.LogDebug($"Subscribing complete for {exchangeName}.");
            });
        }

        public async Task HuobiSnapshot(IOrderbook market)
        {
            var httpClient = new HttpClient();
            var snapshot = JsonDocument.ParseAsync(httpClient.GetStreamAsync($"https://api.huobi.pro/market/depth?symbol={market.Symbol.ToLower()}&type=step1&depth=10").Result).Result.RootElement;
            var bids = snapshot.GetProperty("tick").GetProperty("bids").EnumerateArray();
            foreach (var bid in bids)
            {
                decimal price = bid[0].GetDecimal();
                decimal size = bid[1].GetDecimal();
                market.OfficialBids.TryAdd(price, size);
            }
            var asks = snapshot.GetProperty("tick").GetProperty("asks").EnumerateArray();
            foreach (var ask in asks)
            {
                decimal price = ask[0].GetDecimal();
                decimal size = ask[1].GetDecimal();
                market.OfficialAsks.TryAdd(price, size);
            }
        }
    }
}