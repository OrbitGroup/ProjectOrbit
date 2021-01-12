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
        public string TickerRestApi { get; set; } = "https://api.huobi.pro/v1/common/symbols"; //dictionary of the REST API calls which pull all symbols for the exchanges
        public string SocketClientApi { get; set; } = "wss://api.huobi.pro/ws";
        public JsonElement.ArrayEnumerator Tickers { get; set; }
        public List<IClientWebSocket> Clients { get; set; } = new List<IClientWebSocket>();
        public IClientWebSocket Client { get; set; }
        public int MaxMarketsPerClient { get; } = 30;

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
        public void GetTickers() //each exchange requires a different method to parse their REST API output
        {
            var httpClient = new HttpClient();
            var rootElement = JsonDocument.ParseAsync(httpClient.GetStreamAsync(TickerRestApi).Result).Result.RootElement;
            Tickers = rootElement.GetProperty("data").EnumerateArray();
            httpClient.Dispose();

        }

        public async Task<WebSocketAdapter> GetExchangeClientAsync()
        {
            var client = new ClientWebSocket();
            var factory = new LoggerFactory();
            var adapter = new WebSocketAdapter(factory.CreateLogger<WebSocketAdapter>(), client);

            client.Options.KeepAliveInterval = new TimeSpan(0, 0, 5);
            await client.ConnectAsync(new Uri(SocketClientApi), CancellationToken.None);
            adapter.TimeStarted = DateTime.UtcNow;
            Client = adapter;
            return adapter;
        }

        public Task Snapshot(IOrderbook Market)
        {
            var httpClient = new HttpClient();
            var snapshot = JsonDocument.ParseAsync(httpClient.GetStreamAsync($"https://api.huobi.pro/market/depth?symbol={Market.Symbol.ToLower()}&type=step1&depth=10").Result).Result.RootElement;
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

            return Task.CompletedTask;
        }

        public async Task Subscribe(List<IOrderbook> Markets)
        {
            foreach (var market in Markets)
            {
                await Snapshot(market);
                await Client.SendAsync(new ArraySegment<byte>(
                            Encoding.ASCII.GetBytes($"{{\"sub\": \"market.{market.Symbol.ToLower()}.mbp.150\",\n  \"id\": \"id{ID}\"\n }}")
                            ), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                ID++;
            }

            Exchange.Clients.Add(Client);
        }
    }
}