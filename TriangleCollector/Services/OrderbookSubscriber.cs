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
            Parallel.ForEach(ProjectOrbit.Exchanges, async (exchange) =>
            {
                await CreateNewListenerAsync(exchange, stoppingToken);
                
                while (!stoppingToken.IsCancellationRequested) //structured on a continuous subscription basis as opposed to a batched approached since the Subscription Manager will give rise to continuous individual subscriptions
                {
                    try
                    {
                        if (exchange.ExchangeClient.Client.Markets.Count < exchange.ExchangeClient.MaxMarketsPerClient && exchange.ExchangeClient.Client.State == WebSocketState.Open)
                        {
                            if (exchange.SubscriptionQueue.TryDequeue(out var market) && market != null)
                            {
                                await exchange.ExchangeClient.Subscribe(market);
                                if(exchange.SubscribedMarkets.TryAdd(market.Symbol, market))
                                {
                                    //_logger.LogInformation($"successfully subscribed to {market.Symbol}");
                                } else
                                {
                                    _logger.LogError($"error for {exchange.ExchangeName}: subscribed to market that is already subscribed {market.Symbol}");
                                }
                            }
                        }
                        else if (!(exchange.ExchangeClient.Client.Markets.Count < exchange.ExchangeClient.MaxMarketsPerClient)) //initialize a new client/listener if the current client reached it's maximum number of markets without a websocket disconnection or exception
                        {
                            await CreateNewListenerAsync(exchange, stoppingToken);

                        } else if (exchange.ExchangeClient.Client.State != WebSocketState.Open) //handles a subscription issue due to disconnection
                        {
                            exchange.ExchangeClient.Client.Markets.ForEach(m => exchange.SubscribedMarkets.TryRemove(m.Symbol, out var _));
                            exchange.ExchangeClient.Client.Markets.ForEach(m => exchange.SubscriptionQueue.Enqueue(m));
                            await CreateNewListenerAsync(exchange, stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message == "Unable to connect to the remote server" || ex.Message == "The WebSocket is in an invalid state ('Aborted') for this operation. Valid states are: 'Open, CloseReceived'"
                            || ex.Message == "Unable to read data from the transport connection: An established connection was aborted by the software in your host machine.."
                            || ex.Message == "Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host..")
                        {
                            _logger.LogWarning($"Websocket exception encountered during subscription process. Initializing new client and connection");
                            foreach (var market in exchange.ExchangeClient.Client.Markets)
                            {
                                if (exchange.SubscribedMarkets.TryRemove(market.Symbol, out var _))
                                {
                                    exchange.SubscriptionQueue.Enqueue(market);
                                }
                            }
                            await CreateNewListenerAsync(exchange, stoppingToken);
                        }
                        else
                        {
                            _logger.LogError(ex.ToString());
                        }
                    }
                    if(exchange.SubscribedMarkets.Count > (exchange.Clients.Where(c=>c.State == WebSocketState.Open).Count() * exchange.ExchangeClient.MaxMarketsPerClient))
                    {
                        _logger.LogWarning($"{exchange.ExchangeName}: Paring SubscribedMarkets list for unhandled websocket disconnections.");
                        var abortedClients = exchange.Clients.Where(c => c.State != WebSocketState.Open);
                        foreach(var client in abortedClients)
                        {
                            foreach(var market in client.Markets)
                            {
                                if(exchange.SubscribedMarkets.TryRemove(market.Symbol, out var _))
                                {
                                    exchange.SubscriptionQueue.Enqueue(market);
                                }
                            }
                        }
                    }
                }
            });
        }
        public async Task CreateNewListenerAsync(IExchange exchange, CancellationToken stoppingtoken)
        {
            try
            {
                await exchange.ExchangeClient.GetExchangeClientAsync();
                var listener = new OrderbookListener(_factory.CreateLogger<OrderbookListener>(), exchange.ExchangeClient.Client, exchange);
                await listener.StartAsync(stoppingtoken);
            } catch (Exception ex)
            {
                await Task.Delay(3000);
                await CreateNewListenerAsync(exchange, stoppingtoken);
            }
            
        }
    }
}