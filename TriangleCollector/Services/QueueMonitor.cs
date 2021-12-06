using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models.Interfaces;
using System.Linq;
using System.Collections.Generic;
using TriangleCollector.Models;
using Microsoft.ApplicationInsights;

namespace TriangleCollector.Services
{
    public class QueueMonitor : BackgroundService
    {
        private readonly ILoggerFactory _factory;

        private readonly ILogger<QueueMonitor> _logger;
        private readonly TelemetryClient _telemetryClient;
        private int CalculatorCount = 1;

        private readonly int MaxTriangleCalculators = 7;

        private IExchange Exchange { get; set; }

        public QueueMonitor(ILoggerFactory factory, ILogger<QueueMonitor> logger, TelemetryClient telemetryClient, IExchange exch)
        {
            _factory = factory;
            _logger = logger;
            _telemetryClient = telemetryClient;
            Exchange = exch;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting Queue Monitor...");

            stoppingToken.Register(() => _logger.LogDebug("Stopping Queue Monitor..."));
            await BackgroundProcessing(stoppingToken);
            _logger.LogDebug("Stopped Queue Monitor.");
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (Exchange.TrianglesToRecalculate.Count > Exchange.SubscribedMarkets.Count && CalculatorCount < MaxTriangleCalculators)//start a new calculator if the queue is >75% its theoretical maximum
                {
                    //TODO: implement average queue size metric to decrement TriangleCalculators.
                    CalculatorCount++;
                    var newCalc = new TriangleCalculator(_factory.CreateLogger<TriangleCalculator>(), _telemetryClient, CalculatorCount, Exchange);
                    await newCalc.StartAsync(stoppingToken);
                    
                }
                await Task.Delay(5000);
            }
        }
    }
}
