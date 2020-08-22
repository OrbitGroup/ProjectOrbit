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

        private double timeWasters = 0;

        private double totalCalculations = 0;

        private double percentWasted = 0;

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

            while (!stoppingToken.IsCancellationRequested)
            {
                if (TriangleCollector.TrianglesToRecalculate.TryDequeue(out Triangle triangle))
                {
                    TriangleCollector.OfficialOrderbooks.TryGetValue(triangle.FirstSymbol, out Orderbook firstSymbolOrderbook);
                    TriangleCollector.OfficialOrderbooks.TryGetValue(triangle.SecondSymbol, out Orderbook secondSymbolOrderbook);
                    TriangleCollector.OfficialOrderbooks.TryGetValue(triangle.ThirdSymbol, out Orderbook thirdSymbolOrderbook);

                    if (firstSymbolOrderbook != null)
                    {
                        triangle.FirstSymbolOrderbook = firstSymbolOrderbook;
                    }

                    if (secondSymbolOrderbook != null)
                    {
                        triangle.SecondSymbolOrderbook = secondSymbolOrderbook;
                    }

                    if (thirdSymbolOrderbook != null)
                    {
                        triangle.ThirdSymbolOrderbook = thirdSymbolOrderbook;
                    }

                    if (triangle.AllOrderbooksSet)
                    {
                        //_logger.LogDebug($"RECALCULATING TRIANGLE: {triangle.FirstSymbol}: {triangle.FirstSymbolAsk} - {triangle.SecondSymbol}: {triangle.SecondSymbolBid} - {triangle.ThirdSymbol}: {triangle.ThirdSymbolBid}");
                        triangle.SetMaxVolumeAndProfitability();
                        //var reversedProfit = triangle.GetReversedProfitability();
                        //TriangleCollector.Triangles.TryGetValue(triangle.ToString(), out decimal oldEntry);
                        TriangleCollector.Triangles.AddOrUpdate(triangle.ToString(), triangle.ProfitPercent, (key, oldValue) => oldValue = triangle.ProfitPercent);
                        var newestTimestamp = new List<DateTime> { firstSymbolOrderbook.timestamp, secondSymbolOrderbook.timestamp, thirdSymbolOrderbook.timestamp }.Max();
                        //TriangleCollector.Triangles.TryGetValue(triangle.ToString(), out decimal newEntry);
                        //totalCalculations++;
                        //if (newEntry == oldEntry)
                        //{
                        //    timeWasters++;
                        //}
                        //percentWasted = (timeWasters / totalCalculations) * 100;
                        //_logger.LogDebug($"Total calcs: {totalCalculations} | Time wasters: {timeWasters} | % time wasters {percentWasted}");
                        TriangleCollector.TriangleRefreshTimes.AddOrUpdate(triangle.ToString(), newestTimestamp, (key, oldValue) => oldValue = newestTimestamp);
                        TriangleCollector.RecalculatedTriangles.Enqueue(triangle);
                    }
                }
            }
        }
    }
}
