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

namespace TriangleCollector.Models.Exchanges.Hitbtc
{
    public class HitbtcClient : IExchangeClient
    {
        public IExchange Exchange { get; set; }
        public string TickerRestApi { get; set; } = "https://api.hitbtc.com/api/2/public/symbol"; //dictionary of the REST API calls which pull all symbols for the exchanges
        public string SocketClientApi { get; set; } = "wss://api.hitbtc.com/api/2/ws";
        public JsonElement.ArrayEnumerator Tickers { get; set; }
        public List<IClientWebSocket> Clients { get; set; } = new List<IClientWebSocket>();
        public IClientWebSocket Client { get; set; }
        public int MaxMarketsPerClient { get; } = 40;

        //public BinanceClient() //to add a new exchange to Orbit, append the list below with the proper REST API URL.
        //{
        //    TickerRESTAPI.Add("hitbtc", "");
        //    TickerRESTAPI.Add("binance", "");
        //    TickerRESTAPI.Add("bittrex", "https://api.bittrex.com/v3/markets");
        //    TickerRESTAPI.Add("huobi", "https://api.huobi.pro/v1/common/symbols");
        //    SocketClientAPI.Add("hitbtc", "");
        //    SocketClientAPI.Add("binance", "");
        //    SocketClientAPI.Add("bittrex", "https://socket-v3.bittrex.com/signalr");
        //    SocketClientAPI.Add("huobi", "wss://api.huobi.pro/ws");
        //    SocketClientAPI.Add("bitstamp", "wss://ws.bitstamp.net");
        //    PingRESTAPI();
        //}
        public void GetTickers() //each exchange requires a different method to parse their REST API output
        {
            var httpClient = new HttpClient();
            Tickers = JsonDocument.ParseAsync(httpClient.GetStreamAsync(TickerRestApi).Result).Result.RootElement.EnumerateArray();
            httpClient.Dispose();
        }

        public async Task<WebSocketAdapter> GetExchangeClientAsync()
        {
            var client = new ClientWebSocket();
            var factory = new LoggerFactory();
            var adapter = new WebSocketAdapter(factory.CreateLogger<WebSocketAdapter>(), client);

            client.Options.KeepAliveInterval = new TimeSpan(0, 0, 5);
            await client.ConnectAsync(new Uri(SocketClientApi), CancellationToken.None);
            Client = adapter;
            return adapter;
        }

        public async Task Subscribe(List<IOrderbook> Markets)
        {
            foreach (var market in Markets)
            {
                await Client.SendAsync(
                    new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{{\"method\": \"subscribeOrderbook\",\"params\": {{ \"symbol\": \"{market.Symbol}\" }} }}")),
                    WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            }

            Exchange.Clients.Add(Client);
        }
    }
}
