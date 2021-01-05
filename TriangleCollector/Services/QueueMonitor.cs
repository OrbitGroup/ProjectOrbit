using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using TriangleCollector.Models.Exchange_Models;

namespace TriangleCollector.Services
{
    public class QueueMonitor : BackgroundService
    {
        private int QueueSizeTarget = 10;

        private readonly ILoggerFactory _factory;

        private readonly ILogger<QueueMonitor> _logger;

        private int calculatorCount = 1;

        private int MaxTriangleCalculatorQueueLength = 1000;

        private int MaxTriangleCalculators = 7;

        private int NumberOfSecondsUntilStale = 60;

        private Exchange exchange { get; set; }

        public QueueMonitor(ILoggerFactory factory, ILogger<QueueMonitor> logger, Exchange exch)
        {
            _factory = factory;
            _logger = logger;
            exchange = exch;
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
                if (exchange.TrianglesToRecalculate.Count > MaxTriangleCalculatorQueueLength && calculatorCount < MaxTriangleCalculators)
                {
                    //TODO: implement average queue size metric to decrement TriangleCalculators.
                    calculatorCount++;
                    var newCalc = new TriangleCalculator(_factory.CreateLogger<TriangleCalculator>(), calculatorCount, exchange);
                    await newCalc.StartAsync(stoppingToken);
                }
                await Task.Delay(5000);
            }
        }
    }
}
