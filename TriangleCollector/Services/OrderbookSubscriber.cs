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
                try
                {
                    var sw = new Stopwatch();
                    while(!stoppingToken.IsCancellationRequested)
                    {
                        while(exchange.SubscriptionQueue.Count > 0)
                        {
                            var marketCount = Math.Min(exchange.ExchangeClient.MaxMarketsPerClient, exchange.SubscriptionQueue.Count); //for each loop, we subscribe to the lesser of the per-client-maximum and the number of markets in the queue
                            _logger.LogInformation($"Starting subscribing to {marketCount} markets on {exchange.ExchangeName}");
                            sw.Reset();
                            sw.Start();

                            var client = await exchange.ExchangeClient.GetExchangeClientAsync();
                            var listener = new OrderbookListener(_factory.CreateLogger<OrderbookListener>(), client, exchange);
                            await listener.StartAsync(stoppingToken);
                            var markets = new List<IOrderbook>();
                            for (int i = 0; i < marketCount; i++ ) 
                            {
                                var market = exchange.SubscriptionQueue.Dequeue();
                                markets.Add(market);
                            }
                            listener.Markets = markets; //store the markets in the Listener object for this client
                            await exchange.ExchangeClient.Subscribe(markets);
                            sw.Stop();
                            _logger.LogInformation($"Took {sw.ElapsedMilliseconds}ms to subscribe on {exchange.ExchangeName}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"issue with subscribing on {exchange.ExchangeName}");
                    _logger.LogError(ex.Message);
                    throw ex;
                }
                _logger.LogDebug($"Subscribing complete for {exchange.ExchangeName}.");
            });
        }
    }
}