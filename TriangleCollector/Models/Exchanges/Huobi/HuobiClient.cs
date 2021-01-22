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

        private HttpClient HttpClient = new HttpClient();

        public int ID = 1;

        public HuobiClient()
        {
            HttpClient.Timeout = TimeSpan.FromSeconds(10);
        }

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
            var symbols = JsonDocument.ParseAsync(HttpClient.GetStreamAsync(SymbolsRestApi).Result).Result.RootElement.GetProperty("data").EnumerateArray();
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
            var tickerPrices = JsonDocument.ParseAsync(HttpClient.GetStreamAsync(PricesRestApi).Result).Result.RootElement.GetProperty("data").EnumerateArray();
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
                var snapshot = JsonDocument.ParseAsync(HttpClient.GetStreamAsync($"https://api.huobi.pro/market/depth?symbol={Market.Symbol.ToLower()}&type=step1&depth=10").Result).Result.RootElement;
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
                Console.WriteLine("broke on snapshot");
                Console.WriteLine(ex);
                HttpClient.Dispose();
                HttpClient = new HttpClient();
                HttpClient.Timeout = TimeSpan.FromSeconds(10);
            }
            
            return Task.CompletedTask;
        }

        public async Task Subscribe(IOrderbook market)
        {  
            if (Client.State == WebSocketState.Open)
            {
                await Snapshot(market);
                await Client.SendAsync(new ArraySegment<byte>(
                            Encoding.ASCII.GetBytes($"{{\"sub\": \"market.{market.Symbol.ToLower()}.mbp.150\",\n  \"id\": \"id{ID}\"\n }}")
                            ), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                    //there is a rate limit for snapshots of 10 per second
                ID++;
                Client.Markets.Add(market);
            } else
            {
                Exchange.SubscriptionQueue.Enqueue(market); //add these markets back to the queue
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