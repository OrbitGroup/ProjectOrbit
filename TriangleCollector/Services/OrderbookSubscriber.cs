using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models.Interfaces;

namespace TriangleCollector.Services
{
    public class OrderbookSubscriber : BackgroundService
    {
        private readonly ILogger<OrderbookSubscriber> _logger;

        private readonly ILoggerFactory _factory;

        private IExchange Exchange { get; set; }

        public OrderbookSubscriber(ILoggerFactory factory, ILogger<OrderbookSubscriber> logger, IExchange exch)
        {
            _logger = logger;
            _factory = factory;
            Exchange = exch;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug($"Starting Orderbook Subscriber for {Exchange.ExchangeName}...");

            stoppingToken.Register(() => _logger.LogDebug("Stopping Orderbook Subscriber..."));
            await Task.Run(async () =>
            {
                BackgroundProcessing(stoppingToken);
            }, stoppingToken);
        }

        public async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
                await CreateNewListenerAsync(stoppingToken);
                
                while (!stoppingToken.IsCancellationRequested) //structured on a continuous subscription basis as opposed to a batched approached since the Subscription Manager will give rise to continuous individual subscriptions
                {
                    try
                    {
                        if (Exchange.ExchangeClient.Client.Markets.Count < Exchange.ExchangeClient.MaxMarketsPerClient && Exchange.ExchangeClient.Client.State == WebSocketState.Open)
                        {
                            if (Exchange.SubscriptionQueue.TryDequeue(out var market) && market != null)
                            {
                                if(!Exchange.SubscribedMarkets.ContainsKey(market.Symbol))
                                {
                                    await Exchange.ExchangeClient.Subscribe(market);
                                    Exchange.SubscribedMarkets.TryAdd(market.Symbol, market);
                                }
                            }
                        }
                        else if (!(Exchange.ExchangeClient.Client.Markets.Count < Exchange.ExchangeClient.MaxMarketsPerClient)) //initialize a new client/listener if the current client reached it's maximum number of markets without a websocket disconnection or exception
                        {
                            await CreateNewListenerAsync(stoppingToken);

                        } else if (Exchange.ExchangeClient.Client.State != WebSocketState.Open) //handles a subscription issue due to disconnection
                        {
                            foreach (var market in Exchange.ExchangeClient.Client.Markets)
                            {
                                if (Exchange.SubscribedMarkets.TryRemove(market.Symbol, out var _))
                                {
                                    Exchange.SubscriptionQueue.Enqueue(market);
                                }
                            }
                            await CreateNewListenerAsync(stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message == "Unable to connect to the remote server" || ex.Message == "The WebSocket is in an invalid state ('Aborted') for this operation. Valid states are: 'Open, CloseReceived'"
                            || ex.Message == "Unable to read data from the transport connection: An established connection was aborted by the software in your host machine.."
                            || ex.Message == "Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host..")
                        {
                            _logger.LogWarning($"Websocket exception encountered during subscription process for {Exchange.ExchangeName}. Initializing new client and connection");
                            foreach (var market in Exchange.ExchangeClient.Client.Markets)
                            {
                                if (Exchange.SubscribedMarkets.TryRemove(market.Symbol, out var _))
                                {
                                    Exchange.SubscriptionQueue.Enqueue(market);
                                }
                            }
                            await CreateNewListenerAsync(stoppingToken);
                        }
                        else
                        {
                            _logger.LogError(ex.ToString());
                        }
                    }
                    if(Exchange.SubscribedMarkets.Count > (Exchange.Clients.Where(c=>c.State == WebSocketState.Open).Count() * Exchange.ExchangeClient.MaxMarketsPerClient))
                    {
                        _logger.LogWarning($"{Exchange.ExchangeName}: Paring SubscribedMarkets list for unhandled websocket disconnections.");
                        var abortedClients = Exchange.Clients.Where(c => c.State != WebSocketState.Open);
                        foreach(var client in abortedClients)
                        {
                            foreach(var market in client.Markets)
                            {
                                if(Exchange.SubscribedMarkets.TryRemove(market.Symbol, out var _))
                                {
                                    Exchange.SubscriptionQueue.Enqueue(market);
                                }
                            }
                        }
                    }
                }
            
        }
        public async Task CreateNewListenerAsync(CancellationToken stoppingtoken)
        {
            try
            {
                await Exchange.ExchangeClient.GetExchangeClientAsync();
                var listener = new OrderbookListener(_factory.CreateLogger<OrderbookListener>(), Exchange.ExchangeClient.Client, Exchange);
                await listener.StartAsync(stoppingtoken);
            } catch (Exception ex)
            {
                await Task.Delay(3000);
                await CreateNewListenerAsync(stoppingtoken);
            }
            
        }
    }
}