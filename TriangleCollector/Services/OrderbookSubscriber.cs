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
using System.Net.Http;

namespace TriangleCollector.Services
{
    public class OrderbookSubscriber : BackgroundService
    {
        private readonly ILogger<OrderbookSubscriber> _logger;

        private readonly ILoggerFactory _factory;

        private readonly int MaxPairsPerClient = 20;

        private int CurrentClientPairCount = 0;

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
                BackgroundProcessing(stoppingToken);
            }, stoppingToken);
        }

        public void BackgroundProcessing(CancellationToken stoppingToken)
        {
            Parallel.ForEach(TriangleCollector.Exchanges, async (exchange) =>
            {
                string exchangeName = exchange.ExchangeName;
                //_logger.LogDebug($"{exchange.exchangeName}: Subscribing to {exchange.triarbEligibleMarkets.Count()} markets.");

                var client = await ExchangeAPI.GetExchangeClientAsync(exchangeName);
                var listener = new OrderbookListener(_factory.CreateLogger<OrderbookListener>(), client, exchange);
                await listener.StartAsync(stoppingToken);

                //we also start a TriangleCalculator for each exchange here so that we are ready to dequeue and calculate triangles as soon as the subscriptions are intialized.
                var calculator = new TriangleCalculator(_factory.CreateLogger<TriangleCalculator>(), exchange);
                await calculator.StartAsync(stoppingToken);

                //also a QueueMonitor for each exchange

                var monitor = new QueueMonitor(_factory, _factory.CreateLogger<QueueMonitor>(), exchange);
                await monitor.StartAsync(stoppingToken);


                foreach (var market in exchange.TriarbEligibleMarkets)
                {
                    try
                    {
                        int ID = 1;
                        if (CurrentClientPairCount > MaxPairsPerClient)
                        {
                            CurrentClientPairCount = 0;
                            client = await ExchangeAPI.GetExchangeClientAsync(exchangeName);
                            listener = new OrderbookListener(_factory.CreateLogger<OrderbookListener>(), client, exchange);
                            await listener.StartAsync(stoppingToken);
                        }
                        var cts = new CancellationToken();
                        if (exchangeName == "hitbtc") //hitbtc provdes both a snapshot of the orderbook and subsequent updates.
                        {
                            await client.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{{\"method\": \"subscribeOrderbook\",\"params\": {{ \"symbol\": \"{market.Symbol}\" }} }}")), WebSocketMessageType.Text, true, cts);
                        }
                        else if (exchangeName == "binance") //binance's websocket doesn't provide a snapshot of the orderbook; you must create your own snapshot first by requesting a rest API response for every market. The websocket then provides subsequent updates.
                        {
                            await BinanceSnapshot(market); //get snapshot via REST api
                            await client.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{{\"method\": \"SUBSCRIBE\",\"params\": [\"{market.Symbol.ToLower()}@depth@100ms\"], \"id\": {ID} }}")), WebSocketMessageType.Text, true, cts);
                            await Task.Delay(500); //wait 500 ms for the connection to be established
                        }
                        else if (exchangeName == "huobi")
                        {
                            await HuobiSnapshot(market); //same logic/rationale as binance
                            await client.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{{\"sub\": \"market.{market.Symbol.ToLower()}.mbp.150\",\n  \"id\": \"id{ID}\"\n }}")), WebSocketMessageType.Text, true, cts);
                            //await Task.Delay(500);
                        }
                        //_logger.LogDebug($"{exchange.ExchangeName}: subscribed to {market.Symbol}");
                        ID++;
                        CurrentClientPairCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"issue with subscribing to {market.Symbol} on {exchange.ExchangeName}");
                        _logger.LogError(ex.Message);
                        throw ex;
                    }
                }
                _logger.LogDebug($"Subscribing complete for {exchangeName}.");
            });
        }

        //TODO: merge the logic for snapshots into one method, and/or keep this in a seperate class.
        public async Task BinanceSnapshot(Orderbook market)
        {
            var httpClient = new HttpClient();
            var snapshot = JsonDocument.ParseAsync(httpClient.GetStreamAsync($"https://api.binance.com/api/v3/depth?symbol={market.Symbol}&limit=1000").Result).Result.RootElement;
            var bids = snapshot.GetProperty("bids").EnumerateArray();
            foreach (var bid in bids)
            {
                string price = bid[0].GetString();
                decimal priceDecimal = Convert.ToDecimal(price);
                string size = bid[1].GetString();
                decimal sizeDecimal = Convert.ToDecimal(size);

                market.OfficialBids.TryAdd(priceDecimal, sizeDecimal);
            }
            var asks = snapshot.GetProperty("asks").EnumerateArray();
            foreach (var ask in asks)
            {
                string price = ask[0].GetString();
                decimal priceDecimal = Convert.ToDecimal(price);
                string size = ask[1].GetString();
                decimal sizeDecimal = Convert.ToDecimal(size);

                market.OfficialAsks.TryAdd(priceDecimal, sizeDecimal);
            }
        }
        public async Task HuobiSnapshot(Orderbook market)
        {
            var httpClient = new HttpClient();
            var snapshot = JsonDocument.ParseAsync(httpClient.GetStreamAsync($"https://api.huobi.pro/market/depth?symbol={market.Symbol.ToLower()}&type=step1&depth=10").Result).Result.RootElement;
            var bids = snapshot.GetProperty("tick").GetProperty("bids").EnumerateArray();
            foreach (var bid in bids)
            {
                decimal price = bid[0].GetDecimal();
                decimal size = bid[1].GetDecimal();
                market.OfficialBids.TryAdd(price, size);
            }
            var asks = snapshot.GetProperty("tick").GetProperty("asks").EnumerateArray();
            foreach (var ask in asks)
            {
                decimal price = ask[0].GetDecimal();
                decimal size = ask[1].GetDecimal();
                market.OfficialAsks.TryAdd(price, size);
            }
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