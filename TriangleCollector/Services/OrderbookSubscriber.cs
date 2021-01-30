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

        private int ListenerID = 1; //identifier for each listener

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
                await BackgroundProcessing(stoppingToken);
            }, stoppingToken);
        }

        public async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
                await CreateNewListenerAsync();
                
                while (!stoppingToken.IsCancellationRequested) 
                {
                    try
                    {
                        if(Exchange.QueuedSubscription == true)
                        {
                            await QueuedSubscriptionHandling();
                        } 
                        else
                        {
                            await AggregateSubscriptionHandling();
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message == "Unable to connect to the remote server" || ex.Message == "The WebSocket is in an invalid state ('Aborted') for this operation. Valid states are: 'Open, CloseReceived'"
                            || ex.Message == "Unable to read data from the transport connection: An established connection was aborted by the software in your host machine.."
                            || ex.Message == "Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host..")
                        {
                            _logger.LogWarning($"Websocket exception encountered during subscription process for {Exchange.ExchangeName}. Initializing new client and connection");
                            await CreateNewListenerAsync();
                        }
                        else
                        {
                            _logger.LogError(ex.ToString());
                        }
                    } 
                }
        }
        public async Task QueuedSubscriptionHandling()
        {
            if (Exchange.ExchangeClient.Client.Markets.Count < Exchange.ExchangeClient.MaxMarketsPerClient && Exchange.ExchangeClient.Client.State == WebSocketState.Open)
            {
                if (Exchange.SubscriptionQueue.TryDequeue(out var market))
                {
                    await Exchange.ExchangeClient.SubscribeViaQueue(market);
                    Exchange.SubscribedMarkets.TryAdd(market.Symbol, market);
                }
            }
            else //initialize a new client/listener if the current client reached it's maximum number of markets or if it's been disconnected
            {
                await CreateNewListenerAsync();
            }
        }
        public async Task AggregateSubscriptionHandling()
        {
            if(Exchange.AggregateStreamOpen == false)
            {
                if(Exchange.ExchangeClient.Client.State != WebSocketState.Open)
                {
                    await CreateNewListenerAsync();
                }
                await Exchange.ExchangeClient.SubscribeViaAggregate();
            }
        }
        public async Task CreateNewListenerAsync()
        {
            try
            {
                var stoppingToken = new CancellationToken(); //create a new cancellation token for every listener
                await Exchange.ExchangeClient.CreateExchangeClientAsync();
                var listener = new OrderbookListener(_factory.CreateLogger<OrderbookListener>(), Exchange.ExchangeClient.Client, Exchange, ListenerID);
                await listener.StartAsync(stoppingToken);
            } catch (Exception ex)
            {
                await Task.Delay(3000);
                await CreateNewListenerAsync();
            }
            ListenerID++;
        }
    }
}