using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TriangleCollector.Models.Interfaces;
using TriangleCollector.Models;
using System.Net.WebSockets;

namespace TriangleCollector.Services
{
    public class ClientManager : BackgroundService
    {
        private readonly ILoggerFactory _factory;

        private readonly ILogger<ClientManager> _logger;

        private IExchange Exchange;

        private readonly int TimeInterval = 1; //time interval in seconds

        public ClientManager(ILoggerFactory factory, ILogger<ClientManager> logger, IExchange exch)
        {
            _factory = factory;
            _logger = logger;
            Exchange = exch;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting Client Manager for {ExchangeName}...", Exchange.ExchangeName);

            stoppingToken.Register(() => _logger.LogDebug("Stopping Client Manager..."));
            await BackgroundProcessing(stoppingToken);
            _logger.LogDebug("Stopped Client Manager.");
        }
        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var activeClientSnapshot = new List<IClientWebSocket>(Exchange.ActiveClients);
                foreach(var activeClient in activeClientSnapshot)
                {
                    if(activeClient.State != WebSocketState.Open)
                    {
                        _logger.LogWarning($"detected unhandled websocket closure");
                        foreach(var market in activeClient.Markets)
                        {
                            Exchange.SubscribedMarkets.TryRemove(market.Symbol, out var _);
                            Exchange.SubscriptionQueue.Enqueue(market);
                        }
                        Exchange.ActiveClients.Remove(activeClient);
                        Exchange.AggregateStreamOpen = false;
                    }
                }
                await Task.Delay(TimeInterval * 1000, stoppingToken);
            }
        }
    }
}
