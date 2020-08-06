using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models;

namespace TriangleCollector.Services
{
    public class TriangleCalculator : BackgroundService
    {
        private readonly ILogger<TriangleCalculator> _logger;

        private int CalculatorId;

        public TriangleCalculator(ILogger<TriangleCalculator> logger)
        {
            _logger = logger;
            CalculatorId = 1;
        }

        public TriangleCalculator(ILogger<TriangleCalculator> logger, int calculatorCount)
        {
            _logger = logger;
            CalculatorId = calculatorCount;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            

            stoppingToken.Register(() => _logger.LogDebug("Stopping Triangle Calculator..."));

            _logger.LogDebug($"Starting Triangle Calculator {CalculatorId}");

            await Task.Run(async () =>
            {
                await BackgroundProcessing(stoppingToken);
            }, stoppingToken);

            _logger.LogDebug("Stopped Triangle Calculator.");
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            //get orderbooks
            //calculate profit
            //push triangle:profit to Triangles
            //push triangle name to UpdatedTriangles

            while (TriangleCollector.client.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
            {
                if (TriangleCollector.TrianglesToRecalculate.TryDequeue(out Triangle triangle))
                {
                    TriangleCollector.OfficialOrderbooks.TryGetValue(triangle.FirstSymbol, out Orderbook firstSymbolOrderbook);
                    TriangleCollector.OfficialOrderbooks.TryGetValue(triangle.SecondSymbol, out Orderbook secondSymbolOrderbook);
                    TriangleCollector.OfficialOrderbooks.TryGetValue(triangle.ThirdSymbol, out Orderbook thirdSymbolOrderbook);

                    if (firstSymbolOrderbook != null)
                    {
                        triangle.FirstSymbolAsk = firstSymbolOrderbook.LowestAsk;
                    }

                    if (secondSymbolOrderbook != null)
                    {
                        triangle.SecondSymbolAsk = secondSymbolOrderbook.LowestAsk;
                        triangle.SecondSymbolBid = secondSymbolOrderbook.HighestBid;
                    }

                    if (thirdSymbolOrderbook != null)
                    {
                        triangle.ThirdSymbolBid = thirdSymbolOrderbook.HighestBid;
                    }

                    if (triangle.AllPricesSet)
                    {
                        var profit = triangle.GetProfitability();
                        //var reversedProfit = triangle.GetReversedProfitability();
                        TriangleCollector.Triangles.AddOrUpdate(triangle.ToString(), profit, (key, oldValue) => oldValue = profit);
                        var newestTimestamp = new List<DateTime> { firstSymbolOrderbook.timestamp, secondSymbolOrderbook.timestamp, thirdSymbolOrderbook.timestamp }.Min();
                        TriangleCollector.TriangleRefreshTimes.AddOrUpdate(triangle.ToString(), newestTimestamp, (key, oldValue) => oldValue = newestTimestamp);
                    }
                }
            }

        }
    }
}
