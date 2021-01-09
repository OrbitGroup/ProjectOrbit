using Microsoft.AspNetCore.Mvc.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models;
using TriangleCollector.Models.Exchange_Models;


namespace TriangleCollector.Services
{
    public class ActivityMonitor: BackgroundService
    {
        private readonly ILogger<ActivityMonitor> _logger;

        private readonly ILoggerFactory _factory;

        private int LoopTimer = 5; //the interval (in seconds) for each printout of the activity monitor

        private Dictionary<string, int> LastOBCounter = new Dictionary<string, int>();
        private Dictionary<string, int> LastTriarbCounter = new Dictionary<string, int>();

        public ActivityMonitor(ILoggerFactory factory, ILogger<ActivityMonitor> logger)
        {
            _logger = logger;
            _factory = factory;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting Activity Monitor...");

            stoppingToken.Register(() => _logger.LogDebug("Stopping Activity Monitor..."));
            await Task.Run(async () =>
            {
                await BackgroundProcessing(stoppingToken);
            }, stoppingToken);
        }
        public async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            foreach(var exchange in TriangleCollector.Exchanges)
            {
                LastOBCounter.Add(exchange.ExchangeName, 0);
                LastTriarbCounter.Add(exchange.ExchangeName, 0);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("*********************************************************************************************************************************************");
                foreach (var exchange in TriangleCollector.Exchanges)
                {
                    var lastOBcount = LastOBCounter[exchange.ExchangeName];
                    var lastTriarbCount = LastTriarbCounter[exchange.ExchangeName];
                    
                    _logger.LogDebug($"{exchange.ExchangeName} --- Data Points Received: {exchange.AllOrderBookCounter}. Data Receipts/Second (last {LoopTimer}s): {(exchange.AllOrderBookCounter - lastOBcount) / LoopTimer}.");
                    _logger.LogDebug($"{exchange.ExchangeName} --- Triarb Opportunities Calculated: {exchange.RecalculatedTriangles.Count()}. Triarb Opportunities/ Second(last {LoopTimer}s): {(exchange.RecalculatedTriangles.Count() - lastTriarbCount) / LoopTimer}");
                    _logger.LogDebug($"{exchange.ExchangeName} --- Queue Size: {exchange.TrianglesToRecalculate.Count()}.");
                    

                    LastOBCounter[exchange.ExchangeName] = Convert.ToInt32(exchange.AllOrderBookCounter);
                    LastTriarbCounter[exchange.ExchangeName] = exchange.RecalculatedTriangles.Count();
                }
                _logger.LogDebug("*********************************************************************************************************************************************");
                await Task.Delay(LoopTimer * 1000);
            }
        }
    }
}
