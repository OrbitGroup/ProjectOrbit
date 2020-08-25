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

namespace TriangleCollector.Services
{
    public class OrderbookSubscriber : BackgroundService
    {
        private readonly ILogger<OrderbookSubscriber> _logger;

        private readonly ILoggerFactory _factory;

        private readonly int MaxSymbols = 3;

        private readonly int MaxPairsPerClient = 40;

        private int CurrentClientPairCount = 0;

        private readonly bool useDummySymbols = true;

        // the dummy list must be in order of trade execution (sorry!)
        private List<string> dummySymbols = new List<string> { "ICXBTC", "ICXETH", "ETHBTC"};

    public OrderbookSubscriber(ILoggerFactory factory, ILogger<OrderbookSubscriber> logger)
        {
            _logger = logger;
            _factory = factory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting Orderbook Subscriber...");

            stoppingToken.Register(() => _logger.LogDebug("Stopping Orderbook Subscriber..."));
            await Task.Run(async () =>
            {
                await BackgroundProcessing(stoppingToken);
            }, stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            if (useDummySymbols == true)
            {
                var dummyTriangle = new Triangle(dummySymbols[0], dummySymbols[1], dummySymbols[2], _factory.CreateLogger<Triangle>());
                foreach (var pair in dummySymbols)
                {
                    TriangleCollector.triangleEligiblePairs.Add(pair);
                    TriangleCollector.SymbolTriangleMapping.AddOrUpdate(pair, new List<Triangle>() { dummyTriangle }, (key, triangleList) =>
                    {
                        if (key == pair)
                        {
                            triangleList.Add(dummyTriangle);
                        }
                        return triangleList;
                    });
                }             
                _logger.LogDebug("Using dummy list of symbols for testing purposes.");
            } 
            else
            {
                _logger.LogDebug("Loading triangular arbitrage eligible symbols.");
                symbolGenerator();
            }

            _logger.LogDebug($"Subscribing to {TriangleCollector.triangleEligiblePairs.Count()} pairs.");

            var client = await TriangleCollector.GetExchangeClientAsync();
            var listener = new OrderbookListener(_factory.CreateLogger<OrderbookListener>(), client);
            await listener.StartAsync(stoppingToken);

            foreach (var symbol in TriangleCollector.triangleEligiblePairs)
            {
                try
                {
                    if (CurrentClientPairCount > MaxPairsPerClient)
                    {
                        CurrentClientPairCount = 0;
                        client = await TriangleCollector.GetExchangeClientAsync();
                        listener = new OrderbookListener(_factory.CreateLogger<OrderbookListener>(), client);
                        await listener.StartAsync(stoppingToken);
                    }
                    var cts = new CancellationToken();
                    await client.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{{\"method\": \"subscribeOrderbook\",\"params\": {{ \"symbol\": \"{symbol}\" }} }}")), WebSocketMessageType.Text, true, cts);
                    CurrentClientPairCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    throw ex;
                }
            }
            _logger.LogDebug("Subscribing complete.");
        }

        private void symbolGenerator()
        {
            var httpClient = new HttpClient();
            var startNetwork = DateTime.UtcNow;
            var symbols = JsonDocument.ParseAsync(httpClient.GetStreamAsync("https://api.hitbtc.com/api/2/public/symbol").Result).Result.RootElement.EnumerateArray();

            httpClient.Dispose();
            int count = 0;
            foreach (var firstSymbol in symbols)
            {
                if (firstSymbol.GetProperty("quoteCurrency").ToString() == "BTC")
                {
                    var firstMarket = firstSymbol.GetProperty("id").ToString();
                    var firstAlt = firstSymbol.GetProperty("baseCurrency").ToString();
                    foreach (var secondSymbol in symbols)
                    {
                        if (secondSymbol.GetProperty("baseCurrency").ToString() == firstAlt && secondSymbol.GetProperty("quoteCurrency").ToString() != "BTC")
                        {
                            var secondMarket = secondSymbol.GetProperty("id").ToString();
                            var secondBase = secondSymbol.GetProperty("quoteCurrency").ToString();
                            foreach (var thirdSymbol in symbols)
                                if (thirdSymbol.GetProperty("quoteCurrency").ToString() == "BTC" &&
                                    thirdSymbol.GetProperty("baseCurrency").ToString() == secondBase)
                                {
                                    var thirdMarket = thirdSymbol.GetProperty("id").ToString();
                                    //Console.WriteLine($"1: {firstMarket} 2: {secondMarket} 3: {thirdMarket}");
                                    count++;

                                    var newTriangle = new Triangle(firstMarket, secondMarket, thirdMarket, _factory.CreateLogger<Triangle>());

                                    TriangleCollector.triangleEligiblePairs.Add(firstMarket);
                                    TriangleCollector.triangleEligiblePairs.Add(secondMarket); 
                                    TriangleCollector.triangleEligiblePairs.Add(thirdMarket);                                   
                                    foreach (var pair in new List<string> { firstMarket, secondMarket, thirdMarket })
                                    {
                                        TriangleCollector.SymbolTriangleMapping.AddOrUpdate(pair, new List<Triangle>() { newTriangle }, (key, triangleList) =>
                                        {
                                            if (key == pair)
                                            {
                                                triangleList.Add(newTriangle);
                                            }
                                            return triangleList;
                                        });
                                    }
                                }
                        }
                    }
                }            
            }
        }
    }
}
