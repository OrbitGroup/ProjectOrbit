using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Services;

namespace TriangleCollector.Services
{
    class ExchangeServiceInitializer : BackgroundService
    {
        private readonly ILoggerFactory _factory;

        private readonly ILogger<ExchangeServiceInitializer> _logger;

        private TelemetryClient _telemetryClient;

        public ExchangeServiceInitializer(ILoggerFactory factory, ILogger<ExchangeServiceInitializer> logger, TelemetryClient telemetryClient)
        {
            _factory = factory;
            _logger = logger;
            _telemetryClient = telemetryClient;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug($"Initializing Exchange Services...");

            stoppingToken.Register(() => _logger.LogDebug("Stopping Exchange Service Initialization..."));
            await BackgroundProcessing(stoppingToken);
            _logger.LogDebug("Stopped Exchange Service Initialization...");
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            foreach(var exchange in ProjectOrbit.Exchanges)
            {
                var subscriptionManager = new SubscriptionManager(_factory, _factory.CreateLogger<SubscriptionManager>(), exchange);
                await subscriptionManager.StartAsync(stoppingToken);

                var clientManager = new ClientManager(_factory, _factory.CreateLogger<ClientManager>(), exchange);
                await clientManager.StartAsync(stoppingToken);

                var calculator = new TriangleCalculator(_factory.CreateLogger<TriangleCalculator>(), exchange);
                await calculator.StartAsync(stoppingToken);

                var queueMonitor = new QueueMonitor(_factory, _factory.CreateLogger<QueueMonitor>(), exchange);
                await queueMonitor.StartAsync(stoppingToken);

                var subscriber = new OrderbookSubscriber(_factory, _factory.CreateLogger<OrderbookSubscriber>(), _telemetryClient, exchange);
                await subscriber.StartAsync(stoppingToken);

                var statsMonitor = new StatisticsMonitor(_factory.CreateLogger<StatisticsMonitor>(), exchange);
                await statsMonitor.StartAsync(stoppingToken);

                var activityMonitor = new ActivityMonitor(_factory, _factory.CreateLogger<ActivityMonitor>(), exchange);
                await activityMonitor.StartAsync(stoppingToken);

                //_logger.LogInformation($"there are {exchange.TradedMarkets.Count} markets traded on {exchange.ExchangeName}. Of these markets, {exchange.TriarbEligibleMarkets.Count} markets interact to form {exchange.UniqueTriangleCount} triangular arbitrage opportunities");
            }
        }
    }
}
