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

        private readonly bool useDummySymbols = false;

        // the dummy list must be in order of trade execution (sorry!)
        private List<string> dummySymbols = new List<string> { "ETCBTC", "ETCETH", "ETHBTC" };

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
                return;
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
                if (firstSymbol.GetProperty("quoteCurrency").ToString() == "BTC" || firstSymbol.GetProperty("baseCurrency").ToString() == "BTC")
                {
                    var firstMarket = firstSymbol.GetProperty("id").ToString();
                    if (firstSymbol.GetProperty("quoteCurrency").ToString() == "BTC")
                    {
                        var firstDirection = "Buy";
                        foreach (var secondSymbol in symbols)
                        {
                            if (secondSymbol.GetProperty("baseCurrency").ToString() == firstSymbol.GetProperty("baseCurrency").ToString() ||
                            secondSymbol.GetProperty("quoteCurrency").ToString() == firstSymbol.GetProperty("baseCurrency").ToString() &&
                            secondSymbol.GetProperty("id").ToString() != firstMarket)
                            {
                                var secondMarket = secondSymbol.GetProperty("id").ToString();
                                if (secondSymbol.GetProperty("baseCurrency").ToString() == firstSymbol.GetProperty("baseCurrency").ToString())
                                {
                                    var secondDirection = "Sell";
                                    foreach (var thirdSymbol in symbols)
                                        if (thirdSymbol.GetProperty("quoteCurrency").ToString() == "BTC" &&
                                            thirdSymbol.GetProperty("baseCurrency").ToString() == secondSymbol.GetProperty("quoteCurrency").ToString())
                                        {
                                            var thirdDirection = "Sell";
                                            var thirdMarket = thirdSymbol.GetProperty("id").ToString();
                                            Console.WriteLine($"{firstDirection} {firstMarket}, {secondDirection} {secondMarket}, {thirdDirection} {thirdMarket}");
                                            var newTriangle = new Triangle(firstMarket, secondMarket, thirdMarket, _factory.CreateLogger<Triangle>());
                                            count++;
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
                                else
                                {
                                    var secondDirection = "Buy";
                                    foreach (var thirdSymbol in symbols)
                                        if (thirdSymbol.GetProperty("quoteCurrency").ToString() == "BTC" &&
                                            thirdSymbol.GetProperty("baseCurrency").ToString() == secondSymbol.GetProperty("baseCurrency").ToString())
                                        {
                                            var thirdDirection = "Sell";
                                            var thirdMarket = thirdSymbol.GetProperty("id").ToString();
                                            Console.WriteLine($"{firstDirection} {firstMarket}, {secondDirection} {secondMarket}, {thirdDirection} {thirdMarket}");
                                            var newTriangle = new Triangle(firstMarket, secondMarket, thirdMarket, _factory.CreateLogger<Triangle>());
                                            count++;
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
                    } else
                    {
                        var firstDirection = "Sell";
                        foreach (var secondSymbol in symbols)
                        {
                            if (secondSymbol.GetProperty("quoteCurrency").ToString() == firstSymbol.GetProperty("quoteCurrency").ToString() && 
                                secondSymbol.GetProperty("id").ToString() != firstMarket)
                            {
                                var secondMarket = secondSymbol.GetProperty("id").ToString();
                                var secondDirection = "Buy";
                                foreach (var thirdSymbol in symbols)
                                    if (thirdSymbol.GetProperty("quoteCurrency").ToString() == "BTC" &&
                                        thirdSymbol.GetProperty("baseCurrency").ToString() == secondSymbol.GetProperty("baseCurrency").ToString())
                                    {
                                        var thirdDirection = "Sell";
                                        var thirdMarket = thirdSymbol.GetProperty("id").ToString();
                                        Console.WriteLine($"{firstDirection} {firstMarket}, {secondDirection} {secondMarket}, {thirdDirection} {thirdMarket}");
                                        var newTriangle = new Triangle(firstMarket, secondMarket, thirdMarket, _factory.CreateLogger<Triangle>());
                                        count++;
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
            Console.WriteLine(count);
        }
    }
}

                           
/*
DIRECTIONAL TRADING - there are three directions: 

STARTING BY SELLING BTC FOR SOMETHING - Bid, ask, bid. Match up Quotes for trade 2, match up bases for 3
First trade - sell BTC for TUSD. ID = BTCTUSD, basecurrency = BTC, quote currency is TUSD
second trade - buy XMR using TUSD. ID = XMRTUSD, basecurrency = XMR, quote currency is TUSD
third trade - sell XMR for BTC. ID = XMRBTC, basecurrency = XMR, quote currency is BTC

STARTING BY USING BTC TO BUY AN ALTCOIN - ask, bid, bid. Match up bases for trade 2, go quote to base for trade 3
first trade - buy LTC using BTC. ID = LTCBTC, basecurrency = LTC, quote currency = BTC
second trade - sell LTC for ETH. ID = LTCETH, basecurrency = LTC, quote currency = ETH
third trade - sell ETH for BTC. ID = ETHBTC, basecurrency = ETH, quote currency = BTC

STARTING BY BUYING ANOTHER BASECOIN WITH BTC - ask, ask, bid. go base to quote for trade 2, match up bases for trade 3
first trade - buy ETH using BTC. ID = ETHBTC, basecurrency = ETH, quote currency = BTC
second trade - buy XVG using ETH. ID = XVGETH, basecurrency = XVG, quote currency = ETH
third trade - sell XVG for BTC. ID = XVGBTC, basecurrency = XVG, quote currency = BTC

GENERAL OBSERVATIONS AND LOGIC:
the first trade must have BTC as either a quote or base currency. GOOD
for the second trade, the quotes or bases must match, OR the quote (coin 2) must equal the base (coin 1) - 3 options. 
the third trade must always have a quote currency of BTC, but either the bases must match, or match the quote (coin 2) to the base (coin 3)

for all markets:
    if the quote currency OR base currency is BTC, that is your first trade
        firstMarket = symbol
        if quoteCurrency = 'BTC'
            firstDirection = Buy
        else
            firstDirection = Sell
    
    for all markets again:
        if the symbol != firstMarket
            if firstDirection = Sell:
                if the quote currency of the second market matches the first market AND the symbol is not the first market, that is your second trade
                    secondMarket = symbol
                    secondDirection = Buy
            if firstDirection = Buy:
                if the base currencies of the second market matches the first market OR the base currency of the first market matches the quote currency of the second market
                    secondMarket = symbol
                    secondDirection = Sell
        
        for all markets one more time:
            if quoteCurrency = 'BTC'
                if the base currency of the second market matches the third market OR the quote currency of the second market matches the base currency of the third market
                    thirdMarket = symbol
                    thirdDirection = Sell



*/