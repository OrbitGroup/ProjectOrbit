using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace TriangleCollector.Models.Exchange_Models
{
    public class ExchangeAPI
    {
        public Dictionary<string, string> TickerRESTAPI = new Dictionary<string, string>(); //dictionary of the REST API calls which pull all symbols for the exchanges
        public static Dictionary<string, string> SocketClientAPI = new Dictionary<string, string>();
        public Dictionary<string, JsonElement.ArrayEnumerator> Tickers = new Dictionary<string, JsonElement.ArrayEnumerator>();
        public static List<WebSocketAdapter> Clients = new List<WebSocketAdapter>();

        public ExchangeAPI() //to add a new exchange to Orbit, append the list below with the proper REST API URL.
        {
            TickerRESTAPI.Add("hitbtc", "https://api.hitbtc.com/api/2/public/symbol");
            TickerRESTAPI.Add("binance", "https://api.binance.com/api/v3/exchangeInfo");
            TickerRESTAPI.Add("bittrex", "https://api.bittrex.com/v3/markets");
            TickerRESTAPI.Add("huobi", "https://api.huobi.pro/v1/common/symbols");
            SocketClientAPI.Add("hitbtc", "wss://api.hitbtc.com/api/2/ws");
            SocketClientAPI.Add("binance", "wss://stream.binance.com:9443/ws");
            SocketClientAPI.Add("bittrex", "https://socket-v3.bittrex.com/signalr");
            SocketClientAPI.Add("huobi", "wss://api.huobi.pro/ws");
            SocketClientAPI.Add("bitstamp", "wss://ws.bitstamp.net");
            PingRESTAPI();
        }
        public void PingRESTAPI() //each exchange requires a different method to parse their REST API output
        {
            foreach(String exchange in TriangleCollector.ExchangeList)
            {
                
                var URL = TickerRESTAPI[exchange];

                if (exchange == "hitbtc")
                {
                    var httpClient = new HttpClient();
                    var symbols = JsonDocument.ParseAsync(httpClient.GetStreamAsync(URL).Result).Result.RootElement.EnumerateArray();
                    Tickers.Add(exchange, symbols);
                    httpClient.Dispose();
                }
                else if (exchange == "binance")
                {
                    var httpClient = new HttpClient();
                    var rootElement = JsonDocument.ParseAsync(httpClient.GetStreamAsync(URL).Result).Result.RootElement;
                    var symbols = rootElement.GetProperty("symbols").EnumerateArray();
                    Tickers.Add(exchange, symbols);
                    httpClient.Dispose();
                }
                else if (exchange == "bittrex")
                {
                    var httpClient = new HttpClient();
                    var symbols = JsonDocument.ParseAsync(httpClient.GetStreamAsync(URL).Result).Result.RootElement.EnumerateArray();
                    Tickers.Add(exchange, symbols);
                    httpClient.Dispose();
                }
                else if (exchange == "huobi")
                {
                    var httpClient = new HttpClient();
                    var rootElement = JsonDocument.ParseAsync(httpClient.GetStreamAsync(URL).Result).Result.RootElement;
                    var symbols = rootElement.GetProperty("data").EnumerateArray();
                    Tickers.Add(exchange, symbols);
                    httpClient.Dispose();
                }
            }
        }
        public static async Task<WebSocketAdapter> GetExchangeClientAsync(string exchange) //establishes initial connection to exchange for websocket
        {
            var URI = SocketClientAPI[exchange];

            var client = new ClientWebSocket();
            var factory = new LoggerFactory();
            var adapter = new WebSocketAdapter(factory.CreateLogger<WebSocketAdapter>(), client);

            client.Options.KeepAliveInterval = new TimeSpan(0, 0, 5);
            await client.ConnectAsync(new Uri(URI), CancellationToken.None);
            Clients.Add(adapter);
            return adapter;
        }
    }
}
