using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models;
using TriangleCollector.Models.Exchange_Models;

namespace TriangleCollector.Services
{
    public class TriangleCalculator : BackgroundService
    {
        private readonly ILogger<TriangleCalculator> _logger;

        private int CalculatorId;

        private double timeWasters = 0;

        private double totalCalculations = 0;

        private double percentWasted = 0;

        private Exchange exchange { get; set; }

        public TriangleCalculator(ILogger<TriangleCalculator> logger, Exchange exch)
        {
            _logger = logger;
            CalculatorId = 1;
            exchange = exch;
        }

        public TriangleCalculator(ILogger<TriangleCalculator> logger, int calculatorCount, Exchange exch)
        {
            _logger = logger;
            CalculatorId = calculatorCount;
            exchange = exch;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {


            stoppingToken.Register(() => _logger.LogDebug("Stopping Triangle Calculator..."));

            _logger.LogDebug($"Starting Triangle Calculator {CalculatorId} for {exchange.exchangeName}");

            await Task.Run(async () =>
            {
                await BackgroundProcessing(stoppingToken);
            }, stoppingToken);

            _logger.LogDebug("Stopped Triangle Calculator.");
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (exchange.TrianglesToRecalculate.TryDequeue(out Triangle triangle))
                {
                    var firstOrderbookSet = exchange.OfficialOrderbooks.TryGetValue(triangle.FirstSymbol, out Orderbook firstSymbolOrderbook);
                    var secondOrderbookSet = exchange.OfficialOrderbooks.TryGetValue(triangle.SecondSymbol, out Orderbook secondSymbolOrderbook);
                    var thirdOrderbookSet = exchange.OfficialOrderbooks.TryGetValue(triangle.ThirdSymbol, out Orderbook thirdSymbolOrderbook);

                    if (firstOrderbookSet && secondOrderbookSet && thirdOrderbookSet)
                    {
                        var updated = triangle.SetMaxVolumeAndProfitability(firstSymbolOrderbook, secondSymbolOrderbook, thirdSymbolOrderbook);
                        if (!updated)
                        {
                            continue;
                        }
                        if (triangle.ProfitPercent > Convert.ToDecimal(0.002) && triangle.MaxVolume > Convert.ToDecimal(0.001) && triangle.Profit != Convert.ToDecimal(0))
                        {
                            Console.WriteLine($"Profitable Triangular Arbitrage Opportunity --- Exchange: {exchange.exchangeName} - Markets: {firstSymbolOrderbook.symbol}, {secondSymbolOrderbook.symbol} , {thirdSymbolOrderbook.symbol} - Profitability: {Math.Round(triangle.ProfitPercent,4)} - Liquidity: {Math.Round(triangle.MaxVolume, 4)} - Profit: {Math.Round(triangle.Profit,4)} BTC. USD Profit: ${Math.Round(triangle.Profit * USDMonitor.BTCUSDPrice,2)}");
                        }

                        //TriangleCollector.Triangles.TryGetValue(triangle.ToString(), out decimal oldEntry);
                        exchange.Triangles.AddOrUpdate(triangle.ToString(), triangle, (key, oldValue) => oldValue = triangle);
                        var newestTimestamp = new List<DateTime> { firstSymbolOrderbook.timestamp, secondSymbolOrderbook.timestamp, thirdSymbolOrderbook.timestamp }.Max();
                        //TriangleCollector.Triangles.TryGetValue(triangle.ToString(), out decimal newEntry);
                        //totalCalculations++;
                        //if (newEntry == oldEntry)
                        //{
                        //    timeWasters++;
                        //}
                        //percentWasted = (timeWasters / totalCalculations) * 100;
                        //_logger.LogDebug($"Total calcs: {totalCalculations} | Time wasters: {timeWasters} | % time wasters {percentWasted}");
                        exchange.TriangleRefreshTimes.AddOrUpdate(triangle.ToString(), newestTimestamp, (key, oldValue) => oldValue = newestTimestamp);
                        exchange.RecalculatedTriangles.Enqueue(triangle); //this is never dequeued
                    }
                }
            }
        }
    }
}
