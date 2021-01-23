using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models.Interfaces;

namespace TriangleCollector.Models.Exchanges.Binance
{
    public class BinanceClient : IExchangeClient
    {
        public IExchange Exchange { get; set; }
        public string SymbolsRestApi { get; set; } = "https://api.binance.com/api/v3/exchangeInfo";
        public string PricesRestApi { get; set; } = "https://api.binance.com/api/v3/ticker/bookTicker";
        public string SocketClientApi { get; set; } = "wss://stream.binance.com:9443/ws";
        public JsonElement.ArrayEnumerator Tickers { get; set; }
        public IClientWebSocket Client { get; set; }
        public int MaxMarketsPerClient { get; } = 20;

        public int ID = 1;

        //public BinanceClient() //to add a new exchange to Orbit, append the list below with the proper REST API URL.
        //{
        //    TickerRESTAPI.Add("hitbtc", "https://api.hitbtc.com/api/2/public/symbol");
        //    TickerRESTAPI.Add("binance", "");
        //    TickerRESTAPI.Add("bittrex", "https://api.bittrex.com/v3/markets");
        //    TickerRESTAPI.Add("huobi", "https://api.huobi.pro/v1/common/symbols");
        //    SocketClientAPI.Add("hitbtc", "wss://api.hitbtc.com/api/2/ws");
        //    SocketClientAPI.Add("binance", "");
        //    SocketClientAPI.Add("bittrex", "https://socket-v3.bittrex.com/signalr");
        //    SocketClientAPI.Add("huobi", "wss://api.huobi.pro/ws");
        //    SocketClientAPI.Add("bitstamp", "wss://ws.bitstamp.net");
        //    PingRESTAPI();
        //}

        public HashSet<IOrderbook> GetMarketsViaRestApi()
        {
            var output = new HashSet<IOrderbook>();
            var symbols = JsonDocument.ParseAsync(ProjectOrbit.StaticHttpClient.GetStreamAsync(SymbolsRestApi).Result).Result.RootElement.GetProperty("symbols").EnumerateArray();
            foreach (var responseItem in symbols)
            {
                if(responseItem.GetProperty("status").ToString() == "TRADING")
                {
                    var market = new BinanceOrderbook();
                    market.Symbol = responseItem.GetProperty("symbol").ToString();
                    market.BaseCurrency = responseItem.GetProperty("baseAsset").ToString();
                    market.QuoteCurrency = responseItem.GetProperty("quoteAsset").ToString();
                    market.Exchange = Exchange;
                    output.Add(market);
                }
            }
            var tickerPrices = JsonDocument.ParseAsync(ProjectOrbit.StaticHttpClient.GetStreamAsync(PricesRestApi).Result).Result.RootElement.EnumerateArray();
            foreach (var ticker in tickerPrices)
            {
                var symbol = ticker.GetProperty("symbol").ToString();
                var bidPrice = Decimal.Parse(ticker.GetProperty("bidPrice").ToString());
                var bidSize = Decimal.Parse(ticker.GetProperty("bidQty").ToString());
                var askPrice = Decimal.Parse(ticker.GetProperty("askPrice").ToString());
                var askSize = Decimal.Parse(ticker.GetProperty("askQty").ToString());

                if (bidPrice > 0 && askPrice > 0 && bidSize > 0 && askSize > 0)
                {
                    output.Where(m => m.Symbol == symbol).First().OfficialBids.TryAdd(bidPrice, bidSize);
                    output.Where(m => m.Symbol == symbol).First().OfficialAsks.TryAdd(askPrice, askSize);
                }

            }
            output = output.Where(m => m.OfficialAsks.Count > 0 && m.OfficialBids.Count > 0).ToHashSet();
            return output;
        }

        public async Task<WebSocketAdapter> GetExchangeClientAsync()
        {
            var client = new ClientWebSocket();
            var factory = new LoggerFactory();
            var adapter = new WebSocketAdapter(factory.CreateLogger<WebSocketAdapter>(), client);

            client.Options.KeepAliveInterval = new TimeSpan(0, 0, 1);
            await client.ConnectAsync(new Uri(SocketClientApi), CancellationToken.None);
            adapter.TimeStarted = DateTime.UtcNow;
            adapter.Markets = new List<IOrderbook>();
            Client = adapter;
            Exchange.Clients.Add(Client);
            await Task.Delay(250); // clients with zero subscriptions are being aborted; give 1/4 second to ensure connection is complete
            return adapter;
        }

        public Task Snapshot(IOrderbook Market)
        {
            Market.OfficialAsks.Clear();
            Market.OfficialBids.Clear();
            try
            {
                var snapshot = JsonDocument.ParseAsync(ProjectOrbit.StaticHttpClient.GetStreamAsync($"https://api.binance.com/api/v3/depth?symbol={Market.Symbol}&limit=100").Result).Result.RootElement;
                var bids = snapshot.GetProperty("bids").EnumerateArray();
                foreach (var bid in bids)
                {
                    string price = bid[0].GetString();
                    decimal priceDecimal = Convert.ToDecimal(price);
                    string size = bid[1].GetString();
                    decimal sizeDecimal = Convert.ToDecimal(size);

                    Market.OfficialBids.TryAdd(priceDecimal, sizeDecimal);
                }
                var asks = snapshot.GetProperty("asks").EnumerateArray();
                foreach (var ask in asks)
                {
                    string price = ask[0].GetString();
                    decimal priceDecimal = Convert.ToDecimal(price);
                    string size = ask[1].GetString();
                    decimal sizeDecimal = Convert.ToDecimal(size);

                    Market.OfficialAsks.TryAdd(priceDecimal, sizeDecimal);
                }
            } catch (Exception ex)
            {
                /*Console.WriteLine("broke on snapshot");
                Console.WriteLine(ex);*/
            }
            
            return Task.CompletedTask;
        }

        public async Task Subscribe(IOrderbook market)
        {
            bool successfulSnapshot = false;
            int snapshotAttempts = 1;
            int timeoutSeconds = 5;
            if (Client.State == WebSocketState.Open)
            {
                var sw = new Stopwatch();
                sw.Start();
                while (!successfulSnapshot && snapshotAttempts < 3)
                {
                    var snapshotTask = Task.Run(() =>
                    {
                        Snapshot(market);
                    });
                    successfulSnapshot = snapshotTask.Wait(TimeSpan.FromSeconds(timeoutSeconds));
                    if (!successfulSnapshot)
                    {
                        snapshotAttempts++;
                        timeoutSeconds = timeoutSeconds * 2;
                        Console.WriteLine($"Binance: snapshot timeout for {market.Symbol}");
                        Console.WriteLine($"Binance: processing Snapshot for {market.Symbol}, attempt #{snapshotAttempts}");
                    }
                }
                sw.Stop();
                if(snapshotAttempts > 1 && successfulSnapshot) { Console.WriteLine($"Binance: took {snapshotAttempts} attempts and {sw.ElapsedMilliseconds}ms to complete snapshot for {market.Symbol}"); }
                sw.Reset();

                if(successfulSnapshot)
                {
                    await Client.SendAsync(new ArraySegment<byte>(
                            Encoding.ASCII.GetBytes($"{{\"method\": \"SUBSCRIBE\",\"params\": [\"{market.Symbol.ToLower()}@depth@100ms\"], \"id\": {ID} }}")
                            ), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                    ID++;
                    Client.Markets.Add(market);
                } else
                {
                    Exchange.SubscriptionQueue.Enqueue(market);
                }
            } else
            {
                Exchange.SubscriptionQueue.Enqueue(market);
            }
            await Task.Delay(250);
        }
    }
}