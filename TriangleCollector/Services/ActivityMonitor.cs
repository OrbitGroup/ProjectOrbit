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

        private int loopCounter = 1;

        private int loopTimer = 5;

        private Dictionary<string, int> lastOBCounter = new Dictionary<string, int>();
        private Dictionary<string, int> lasttriarbCounter = new Dictionary<string, int>();

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
            foreach(var exchange in TriangleCollector.exchanges)
            {
                lastOBCounter.Add(exchange.exchangeName, 0);
                lasttriarbCounter.Add(exchange.exchangeName, 0);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                Console.WriteLine("*********************************************************************************************************************************************");
                foreach (var exchange in TriangleCollector.exchanges)
                {
                    var lastOBcount = lastOBCounter[exchange.exchangeName];
                    var lasttriarbCount = lasttriarbCounter[exchange.exchangeName];
                    
                    Console.WriteLine($"{exchange.exchangeName} --- Data Points Received: {exchange.allOrderBookCounter}. Data Receipts/Second (last 5s): {(exchange.allOrderBookCounter - lastOBcount) / loopTimer}.");
                    Console.WriteLine($"{exchange.exchangeName} --- Triarb Opportunities Calculated: {exchange.RecalculatedTriangles.Count()}. Triarb Opportunities/ Second(last 5s): {(exchange.RecalculatedTriangles.Count() - lasttriarbCount) / loopTimer}");
                    

                    lastOBCounter[exchange.exchangeName] = Convert.ToInt32(exchange.allOrderBookCounter);
                    lasttriarbCounter[exchange.exchangeName] = exchange.RecalculatedTriangles.Count();
                    /*Triarb Opportunities Calculated: { exchange.RecalculatedTriangles.Count()}. Triarb Opportunities/ Second(Session): { exchange.RecalculatedTriangles.Count() / loopCounter / loopTimer}
                    Triarb Queue: { exchange.TrianglesToRecalculate.Count()}
                    ");*/
                }
                Console.WriteLine("*********************************************************************************************************************************************");
                await Task.Delay(loopTimer * 1000);
                loopCounter++;
            }
                
        }
    }
}
