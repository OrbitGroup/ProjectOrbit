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
                    while(!stoppingToken.IsCancellationRequested) //structed on a continuous subscription basis as opposed to a batched approached since the Subscription Manager will give rise to continuous individual subscriptions
                    {
                        var client = await exchange.ExchangeClient.GetExchangeClientAsync(); //initialize the first client/listener
                        var listener = new OrderbookListener(_factory.CreateLogger<OrderbookListener>(), client, exchange);
                        await listener.StartAsync(stoppingToken);
                        while (exchange.SubscriptionQueue.Count > 0)
                        {
                            if(client.Markets.Count < exchange.ExchangeClient.MaxMarketsPerClient && client.State == WebSocketState.Open)
                            {
                                await exchange.ExchangeClient.Subscribe(exchange.SubscriptionQueue.Dequeue());
                            } else //initialize a new client/listener if the current client is aborted or if it's reached it's maximum
                            {
                                client = await exchange.ExchangeClient.GetExchangeClientAsync();
                                listener = new OrderbookListener(_factory.CreateLogger<OrderbookListener>(), client, exchange);
                                await listener.StartAsync(stoppingToken);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"issue with subscribing on {exchange.ExchangeName}");
                    _logger.LogError(ex.Message);
                    throw ex;
                }
            });
        }
    }
}