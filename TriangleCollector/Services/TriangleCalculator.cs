using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models;
using TriangleCollector.Models.Interfaces;

namespace TriangleCollector.Services
{
    public class TriangleCalculator : BackgroundService
    {
        private readonly ILogger<TriangleCalculator> _logger;

        private int CalculatorId;

        private IExchange Exchange { get; set; }

        public TriangleCalculator(ILogger<TriangleCalculator> logger, IExchange exch)
        {
            _logger = logger;
            CalculatorId = 1;
            Exchange = exch;
        }

        public TriangleCalculator(ILogger<TriangleCalculator> logger, int calculatorCount, IExchange exch)
        {
            _logger = logger;
            CalculatorId = calculatorCount;
            Exchange = exch;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {


            stoppingToken.Register(() => _logger.LogDebug("Stopping Triangle Calculator..."));

            _logger.LogDebug($"Starting Triangle Calculator {CalculatorId} for {Exchange.ExchangeName}");

            await Task.Run(async () =>
            {
                await BackgroundProcessing(stoppingToken);
            }, stoppingToken);

            _logger.LogDebug("Stopped Triangle Calculator.");
        }

        private Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var sw = new Stopwatch();
                if (Exchange.TrianglesToRecalculate.TryDequeue(out Triangle triangle))
                {
                    var firstOrderbookSet = Exchange.OfficialOrderbooks.TryGetValue(triangle.FirstSymbol, out IOrderbook firstSymbolOrderbook);
                    var secondOrderbookSet = Exchange.OfficialOrderbooks.TryGetValue(triangle.SecondSymbol, out IOrderbook secondSymbolOrderbook);
                    var thirdOrderbookSet = Exchange.OfficialOrderbooks.TryGetValue(triangle.ThirdSymbol, out IOrderbook thirdSymbolOrderbook);

                    if (firstOrderbookSet && secondOrderbookSet && thirdOrderbookSet && 
                        thirdSymbolOrderbook.OfficialBids.Count > 0 && secondSymbolOrderbook.OfficialAsks.Count > 0 && firstSymbolOrderbook.OfficialAsks.Count > 0
                        && firstSymbolOrderbook.OfficialBids.Count > 0 && secondSymbolOrderbook.OfficialBids.Count > 0)
                    {
                        sw.Start();
                        var updated = triangle.SetMaxVolumeAndProfitability(firstSymbolOrderbook, secondSymbolOrderbook, thirdSymbolOrderbook);
                        if (!updated)
                        {
                            continue;
                        }
                        sw.Stop();

                        if(sw.ElapsedMilliseconds > 50)
                        {
                            //_logger.LogWarning($"Irregular triangle calculation time for {triangle.ToString()}: {sw.ElapsedMilliseconds}ms");
                        }
                        sw.Reset();
                        var oldestTimestamp = new List<DateTime> { firstSymbolOrderbook.Timestamp, secondSymbolOrderbook.Timestamp, thirdSymbolOrderbook.Timestamp }.Min();
                        var age = (DateTime.UtcNow - oldestTimestamp).TotalMilliseconds;

                        if (triangle.ProfitPercent > Convert.ToDecimal(0.002) && triangle.MaxVolume > Convert.ToDecimal(0.001) && triangle.Profit != Convert.ToDecimal(0))
                        {
                            //Console.WriteLine($"Triarb Opportunity on {Exchange.ExchangeName} | Markets: {firstSymbolOrderbook.Symbol}, {secondSymbolOrderbook.Symbol}, {thirdSymbolOrderbook.Symbol} | Profitability: {Math.Round(triangle.ProfitPercent, 4)}% | Liquidity: {Math.Round(triangle.MaxVolume, 4)} BTC | Profit: {Math.Round(triangle.Profit, 4)} BTC, or ${Math.Round(triangle.Profit * USDMonitor.BTCUSDPrice, 2)} | Delay: {age}ms");
                        }

                        Exchange.Triangles.AddOrUpdate(triangle.ToString(), triangle, (key, oldValue) => oldValue = triangle);
                        
                        Exchange.TriangleRefreshTimes.AddOrUpdate(triangle.ToString(), oldestTimestamp, (key, oldValue) => oldValue = oldestTimestamp);
                        Exchange.RecalculatedTriangles.Enqueue(triangle); //this is never dequeued
                    }
                }
            }
            return Task.CompletedTask;
        }
    }
}
