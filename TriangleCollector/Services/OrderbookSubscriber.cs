﻿using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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

        private TelemetryClient _telemetryClient;

        private int ListenerID = 1; //identifier for each listener

        private IExchange Exchange { get; set; }

        public OrderbookSubscriber(ILoggerFactory factory, ILogger<OrderbookSubscriber> logger, TelemetryClient telemetryClient, IExchange exch)
        {
            _logger = logger;
            _factory = factory;
            _telemetryClient = telemetryClient;
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
            using (_telemetryClient.StartOperation<RequestTelemetry>($"OrderbookSubscriber-{Exchange.ExchangeName}"))
            {
                await CreateNewListenerAsync();

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        if (Exchange.QueuedSubscription == true)
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
        }

        public async Task QueuedSubscriptionHandling()
        {
            if (Exchange.ExchangeClient.PublicClient.Markets.Count < Exchange.ExchangeClient.MaxMarketsPerClient && Exchange.ExchangeClient.PublicClient.State == WebSocketState.Open)
            {
                if (Exchange.SubscriptionQueue.TryDequeue(out var market))
                {
                    await Exchange.ExchangeClient.SubscribeViaQueue(market);
                    Exchange.SubscribedMarkets.TryAdd(market.Symbol, market);

                    double activeSubscriptions = Exchange.SubscribedMarkets.Count;
                    double targetSubscriptions = Exchange.SubscribedMarkets.Count + Exchange.SubscriptionQueue.Count;
                    double relevantRatio = Math.Round(targetSubscriptions / Exchange.TradedMarkets.Count, 2) * 100;
                    //_logger.LogInformation($"{DateTime.Now}: {Exchange.ExchangeName} -- Active Subscriptions: {activeSubscriptions} - {Math.Round(activeSubscriptions / targetSubscriptions, 2) * 100}% subscribed. {relevantRatio}% of markets are deemed relevant.");
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
                if(Exchange.ExchangeClient.PublicClient.State != WebSocketState.Open)
                {
                    await CreateNewListenerAsync();
                }
                await Exchange.ExchangeClient.SubscribeViaAggregate();
                var properties = new Dictionary<string, string> { { "ActiveClients", Exchange.ActiveClients.Count.ToString() } };
                _telemetryClient.TrackEvent("AggregateSubscription", properties);
            }
        }

        public async Task CreateNewListenerAsync()
        {
            try
            {
                var stoppingToken = new CancellationToken(); //create a new cancellation token for every listener
                await Exchange.ExchangeClient.CreatePublicExchangeClientAsync();
                var listener = new OrderbookListener(_factory.CreateLogger<OrderbookListener>(), _telemetryClient, Exchange.ExchangeClient.PublicClient, Exchange, ListenerID);
                await listener.StartAsync(stoppingToken);
                ListenerID++;
                var properties = new Dictionary<string, string>
                {
                    { "ListenerId", ListenerID.ToString() },
                    { "ExchangeName", Exchange.ExchangeName }
                };
                _telemetryClient.TrackEvent("CreateNewListenerAsync", properties);
            } 
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create a new listener: {ex.Message}");
            }
        }
    }
}