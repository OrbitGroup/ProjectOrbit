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

        private readonly bool AllSymbols = true;

        private readonly int MaxSymbols = 100;

        private readonly int MaxPairsPerClient = 50;

        private int CurrentClientPairCount = 0;

        private int Count = 0;

        private List<string> AllowedSymbols = new List<string> { "LTCBTC", "ETHBTC", "LTCETH"};

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
            _logger.LogDebug("Loading exchange symbols.");
            LoadExchangeSymbols();
            
            _logger.LogDebug("Loading triangles from symbols.");
            LoadTrianglesFromSymbols();

            TriangleCollector.Pairs = TriangleCollector.SymbolTriangleMapping.Keys.ToList();

            _logger.LogDebug($"Subscribing to {TriangleCollector.Pairs.Count} pairs.");

            var client = await TriangleCollector.GetExchangeClientAsync();
            var listener = new OrderbookListener(_factory.CreateLogger<OrderbookListener>(), client);
            await listener.StartAsync(stoppingToken);
            foreach (var symbol in TriangleCollector.Pairs)
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

        private void LoadExchangeSymbols()
        {
            var httpClient = new HttpClient();

            var symbols = JsonDocument.ParseAsync(httpClient.GetStreamAsync("https://api.hitbtc.com/api/2/public/symbol").Result).Result;

            httpClient.Dispose();
            
            foreach (var symbol in symbols.RootElement.EnumerateArray())
            {
                var id = symbol.GetProperty("id").ToString();
                if (AllowedSymbols.Contains(id) || AllSymbols || Count < MaxSymbols)
                {
                    TriangleCollector.Pairs.Add(id);
                    TriangleCollector.BaseCoins.Add(symbol.GetProperty("quoteCurrency").ToString());
                    TriangleCollector.AltCoins.Add(symbol.GetProperty("baseCurrency").ToString());
                    Count++;
                }
            }
            TriangleCollector.BaseCoins.Remove("BTC"); //BTC is implied
        }

        private void LoadTrianglesFromSymbols()
        {
            var triangles = new List<Triangle>();

            var altBtc = TriangleCollector.Pairs.Where(x => x.EndsWith("BTC")).ToList();
            var altBase = TriangleCollector.Pairs.Where(x => TriangleCollector.BaseCoins.Any(x.Contains) && !x.EndsWith("BTC")).ToList();
            var baseBtc = TriangleCollector.Pairs.Where(x => x.EndsWith("BTC")).ToList();

            foreach (var firstPair in altBtc)
            {
                foreach (var secondPair in altBase)
                {
                    foreach (var thirdPair in baseBtc)
                    {
                        var firstPairAlt = "INVALID";
                        if (firstPair.EndsWith("BTC"))
                        {
                            firstPairAlt = firstPair.Remove(firstPair.Length - 3);
                        }

                        var secondPairBase = "INVALID";
                        if (secondPair.StartsWith(firstPairAlt))
                        {
                            secondPairBase = secondPair.Remove(0, firstPairAlt.Length);
                            if (!TriangleCollector.BaseCoins.Contains(secondPairBase)) secondPairBase = "INVALID";
                        }

                        if (secondPair.Contains(firstPairAlt) && thirdPair.Contains(secondPairBase) && thirdPair.Length == secondPairBase.Length + 3)
                        {
                            var newTriangle = new Triangle(firstPair, secondPair, thirdPair);

                            foreach (var pair in new List<string> { firstPair, secondPair, thirdPair })
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
