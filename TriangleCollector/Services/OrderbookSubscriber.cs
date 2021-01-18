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
                    var client = await exchange.ExchangeClient.GetExchangeClientAsync(); //initialize the first client/listener
                    var listener = new OrderbookListener(_factory.CreateLogger<OrderbookListener>(), client, exchange);
                    await listener.StartAsync(stoppingToken);
                    while (!stoppingToken.IsCancellationRequested) //structured on a continuous subscription basis as opposed to a batched approached since the Subscription Manager will give rise to continuous individual subscriptions
                    {
                        if(client.Markets.Count < exchange.ExchangeClient.MaxMarketsPerClient && client.State == WebSocketState.Open)
                        {
                            if(exchange.SubscriptionQueue.TryDequeue(out var market) && market != null)
                            {
                                try
                                {
                                    await exchange.ExchangeClient.Subscribe(market);
                                } 
                                catch(Exception ex)
                                {
                                    if(ex.Message == "Unable to connect to the remote server" || ex.Message == "The WebSocket is in an invalid state ('Aborted') for this operation. Valid states are: 'Open, CloseReceived'"
                                    || ex.Message == "Unable to read data from the transport connection: An established connection was aborted by the software in your host machine..")
                                    {
                                        _logger.LogWarning($"Websocket exception encountered during subscription process. Initializing new connection");
                                        //_logger.LogWarning($"{ex}");
                                        client = await exchange.ExchangeClient.GetExchangeClientAsync();
                                        listener = new OrderbookListener(_factory.CreateLogger<OrderbookListener>(), client, exchange);
                                        await listener.StartAsync(stoppingToken);
                                        await exchange.ExchangeClient.Subscribe(market);
                                    } else
                                    {
                                        _logger.LogError(ex.ToString());
                                    }
                                        
                                }
                            }
                        } else //initialize a new client/listener if the current client is aborted or if it's reached it's maximum
                        {
                            client = await exchange.ExchangeClient.GetExchangeClientAsync();
                            listener = new OrderbookListener(_factory.CreateLogger<OrderbookListener>(), client, exchange);
                            await listener.StartAsync(stoppingToken);
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