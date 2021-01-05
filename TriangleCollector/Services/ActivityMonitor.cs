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
        private readonly ILogger<OrderbookSubscriber> _logger;

        private readonly ILoggerFactory _factory;

        private int loopCounter = 1;

        private int loopTimer = 5;

        public ActivityMonitor(ILoggerFactory factory, ILogger<OrderbookSubscriber> logger)
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
            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var exchange in TriangleCollector.exchanges)
                {
                    Console.WriteLine($"{exchange.exchangeName} --- Data Points Received: {exchange.allOrderBookCounter}. Data Receipts/Second (Session): {exchange.allOrderBookCounter / loopCounter / loopTimer}.");
                    Console.WriteLine($"{exchange.exchangeName} --- Triarb Opportunities Calculated: {exchange.RecalculatedTriangles.Count()}. Triarb Opportunities/ Second(Session): {exchange.RecalculatedTriangles.Count() / loopCounter / loopTimer}");
                    
                    /*Triarb Opportunities Calculated: { exchange.RecalculatedTriangles.Count()}. Triarb Opportunities/ Second(Session): { exchange.RecalculatedTriangles.Count() / loopCounter / loopTimer}
                    Triarb Queue: { exchange.TrianglesToRecalculate.Count()}
                    ");*/
                }
                await Task.Delay(loopTimer * 1000);
                loopCounter++;
            }
                
        }
    }
}
