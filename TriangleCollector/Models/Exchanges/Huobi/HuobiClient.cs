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

namespace TriangleCollector.Models.Exchanges.Huobi
{
    public class HuobiClient : IExchangeClient
    {
        public IExchange Exchange { get; set; }

        public string SymbolsRestApi { get; set; } = "https://api.huobi.pro/v1/common/symbols"; //dictionary of the REST API calls which pull all symbols for the exchanges

        public string PricesRestApi { get; set; } = "https://api.huobi.pro/market/tickers";

        public string SocketClientApi { get; set; } = "wss://api.huobi.pro/ws";

        public JsonElement.ArrayEnumerator Tickers { get; set; }

        public IClientWebSocket Client { get; set; }

        public int MaxMarketsPerClient { get; } = 30;

        public int ID = 1;

        public async Task<WebSocketAdapter> GetExchangeClientAsync()
        {
            var client = new ClientWebSocket();
            var factory = new LoggerFactory();
            var adapter = new WebSocketAdapter(factory.CreateLogger<WebSocketAdapter>(), client);

            client.Options.KeepAliveInterval = new TimeSpan(0, 0, 5);
            await client.ConnectAsync(new Uri(SocketClientApi), CancellationToken.None);
            adapter.TimeStarted = DateTime.UtcNow;
            adapter.Markets = new List<IOrderbook>();
            Client = adapter;
            Exchange.Clients.Add(Client);
            await Task.Delay(250); // clients with zero subscriptions are being aborted; give 1/4 second to ensure connection is complete
            return adapter;
        }
        public HashSet<IOrderbook> GetMarketsViaRestApi()
        {
            var output = new HashSet<IOrderbook>();
            var symbols = JsonDocument.ParseAsync(ProjectOrbit.StaticHttpClient.GetStreamAsync(SymbolsRestApi).Result).Result.RootElement.GetProperty("data").EnumerateArray();
            foreach (var responseItem in symbols)
            {
                if (responseItem.GetProperty("state").ToString() == "online")
                {
                    var market = new HuobiOrderbook();
                    market.Symbol = responseItem.GetProperty("symbol").ToString().ToUpper();
                    market.BaseCurrency = responseItem.GetProperty("base-currency").ToString().ToUpper();
                    market.QuoteCurrency = responseItem.GetProperty("quote-currency").ToString().ToUpper();
                    market.Exchange = Exchange;
                    output.Add(market);
                }
            }
            var tickerPrices = JsonDocument.ParseAsync(ProjectOrbit.StaticHttpClient.GetStreamAsync(PricesRestApi).Result).Result.RootElement.GetProperty("data").EnumerateArray();
            foreach (var ticker in tickerPrices)
            {
                var symbol = ticker.GetProperty("symbol").ToString().ToUpper();
                if (output.Where(m => m.Symbol == symbol).Count() > 0)
                {
                    var bidPrice = ticker.GetProperty("bid").GetDecimal();
                    var bidSize = ticker.GetProperty("bidSize").GetDecimal();
                    var askPrice = ticker.GetProperty("ask").GetDecimal();
                    var askSize = ticker.GetProperty("askSize").GetDecimal();
                    if (bidPrice > 0 && askPrice > 0 && bidSize > 0 && askSize > 0)
                    {
                        output.Where(m => m.Symbol == symbol).First().OfficialBids.TryAdd(bidPrice, bidSize);
                        output.Where(m => m.Symbol == symbol).First().OfficialAsks.TryAdd(askPrice, askSize);
                    }
                }
            }
            output = output.Where(m => m.OfficialAsks.Count > 0 && m.OfficialBids.Count > 0).ToHashSet();
            return output;
        }
        public Task Snapshot(IOrderbook Market)
        {
            Market.OfficialAsks.Clear();
            Market.OfficialBids.Clear();
            try
            {
                var snapshot = JsonDocument.ParseAsync(ProjectOrbit.StaticHttpClient.GetStreamAsync($"https://api.huobi.pro/market/depth?symbol={Market.Symbol.ToLower()}&type=step1&depth=10").Result).Result.RootElement; 
                var bids = snapshot.GetProperty("tick").GetProperty("bids").EnumerateArray();
                foreach (var bid in bids)
                {
                    decimal price = bid[0].GetDecimal();
                    decimal size = bid[1].GetDecimal();
                    Market.OfficialBids.TryAdd(price, size);
                }
                var asks = snapshot.GetProperty("tick").GetProperty("asks").EnumerateArray();
                foreach (var ask in asks)
                {
                    decimal price = ask[0].GetDecimal();
                    decimal size = ask[1].GetDecimal();
                    Market.OfficialAsks.TryAdd(price, size);
                }
            } catch(Exception ex)
            {
                //Console.WriteLine("broke on snapshot");
                Console.WriteLine(ex);
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
                        Console.WriteLine($"Huobi: snapshot timeout for {market.Symbol}");
                        Console.WriteLine($"Huobi: processing Snapshot for {market.Symbol}, attempt #{snapshotAttempts}");
                    }
                }
                sw.Stop();
                if (snapshotAttempts>1 && successfulSnapshot) { Console.WriteLine($"Huobi: took {snapshotAttempts} attempts and {sw.ElapsedMilliseconds}ms to complete snapshot for {market.Symbol}"); }
                sw.Reset();

                if(successfulSnapshot)
                {
                    await Client.SendAsync(new ArraySegment<byte>(
                            Encoding.ASCII.GetBytes($"{{\"sub\": \"market.{market.Symbol.ToLower()}.mbp.150\",\n  \"id\": \"id{ID}\"\n }}")
                            ), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                    ID++;
                    Client.Markets.Add(market);
                } else
                {
                    Exchange.SubscriptionQueue.Enqueue(market); //add this market back to the queue
                }
            } else
            {
                Exchange.SubscriptionQueue.Enqueue(market); //add this market back to the queue
            }
            await Task.Delay(100);
        }

        public async Task UnSubscribe(IOrderbook market, IClientWebSocket client)
        {
            if (client.State == WebSocketState.Open)
            {
                await Client.SendAsync(new ArraySegment<byte>(
                            Encoding.ASCII.GetBytes($"{{\"sub\": \"market.{market.Symbol.ToLower()}.mbp.150\",\n  \"id\": \"id{ID}\"\n }}")
                            ), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}