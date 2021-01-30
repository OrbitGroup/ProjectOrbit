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
                    sw.Start();
                    triangle.SetMaxVolumeAndProfitability();
                    sw.Stop();

                    if(sw.ElapsedMilliseconds > 50)
                    {
                        _logger.LogWarning($"Irregular triangle calculation time for {triangle.ToString()}: {sw.ElapsedMilliseconds}ms");
                    }
                    sw.Reset();
                    var oldestTimestamp = new List<DateTime> { triangle.FirstSymbolOrderbook.Timestamp, triangle.SecondSymbolOrderbook.Timestamp, triangle.ThirdSymbolOrderbook.Timestamp }.Min();
                    var age = (DateTime.UtcNow - oldestTimestamp).TotalMilliseconds;

                    if(triangle.ProfitPercent > 0)
                    {
                        Exchange.TotalUSDValueProfitableTriangles += (triangle.Profit * USDMonitor.BTCUSDPrice);
                        if(triangle.ProfitPercent>0.02m)
                        {
                            Exchange.TotalUSDValueViableTriangles += (triangle.Profit * USDMonitor.BTCUSDPrice);
                            Exchange.EstimatedViableProfit += (triangle.ProfitPercent - 0.02m) * triangle.MaxVolume;
                        }
                    }


                    //if (triangle.ProfitPercent > Convert.ToDecimal(0.002) && triangle.MaxVolume > Convert.ToDecimal(0.001) && triangle.Profit != Convert.ToDecimal(0))
                    //{
                        //Console.WriteLine($"Triarb Opportunity on {Exchange.ExchangeName} | Markets: {firstSymbolOrderbook.Symbol}, {secondSymbolOrderbook.Symbol}, {thirdSymbolOrderbook.Symbol} | Profitability: {Math.Round(triangle.ProfitPercent, 4)}% | Liquidity: {Math.Round(triangle.MaxVolume, 4)} BTC | Profit: {Math.Round(triangle.Profit, 4)} BTC, or ${Math.Round(triangle.Profit * USDMonitor.BTCUSDPrice, 2)} | Delay: {age}ms");
                    //}
                        
                    Exchange.TriangleRefreshTimes.AddOrUpdate(triangle.ToString(), oldestTimestamp, (key, oldValue) => oldValue = oldestTimestamp);
                    //Exchange.RecalculatedTriangles.Enqueue(triangle); //this is never dequeued
                    
                }
                /*else
                {
                    if (Exchange.TrianglesToRecalculate.Count > 0)
                    {
                        var test = Exchange.TrianglesToRecalculate.TryDequeue(out var result);
                        _logger.LogError("unable to dequeue triangles");
                    }
                }*/
            }
            return Task.CompletedTask;
        }
    }
}
