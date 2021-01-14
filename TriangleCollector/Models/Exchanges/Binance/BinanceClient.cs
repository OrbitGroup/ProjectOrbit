﻿using Microsoft.Extensions.Logging;
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

namespace TriangleCollector.Models.Exchanges.Binance
{
    public class BinanceClient : IExchangeClient
    {
        public IExchange Exchange { get; set; }
        public string TickerRestApi { get; set; } = "https://api.binance.com/api/v3/exchangeInfo"; //dictionary of the REST API calls which pull all symbols for the exchanges
        public string SocketClientApi { get; set; } = "wss://stream.binance.com:9443/ws";
        public JsonElement.ArrayEnumerator Tickers { get; set; }
        public List<IClientWebSocket> Clients { get; set; } = new List<IClientWebSocket>();
        public IClientWebSocket Client { get; set; }
        public int MaxMarketsPerClient { get; } = 20;

        private HttpClient HttpClient = new HttpClient();

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
            Tickers = rootElement.GetProperty("symbols").EnumerateArray();
            httpClient.Dispose();

        }

        public async Task<WebSocketAdapter> GetExchangeClientAsync()
        {
            var client = new ClientWebSocket();
            var factory = new LoggerFactory();
            var adapter = new WebSocketAdapter(factory.CreateLogger<WebSocketAdapter>(), client);

            client.Options.KeepAliveInterval = new TimeSpan(0, 0, 1);
            await client.ConnectAsync(new Uri(SocketClientApi), CancellationToken.None);
            adapter.TimeStarted = DateTime.UtcNow;
            Client = adapter;
            return adapter;
        }

        public Task Snapshot(IOrderbook Market)
        {
            var snapshot = JsonDocument.ParseAsync(HttpClient.GetStreamAsync($"https://api.binance.com/api/v3/depth?symbol={Market.Symbol}&limit=100").Result).Result.RootElement;
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
            return Task.CompletedTask;
        }

        public async Task Subscribe(List<IOrderbook> Markets)
        {
            foreach (var market in Markets)
            {
                if(Client.State == WebSocketState.Open)
                {
                    await Snapshot(market);
                    await Client.SendAsync(new ArraySegment<byte>(
                                Encoding.ASCII.GetBytes($"{{\"method\": \"SUBSCRIBE\",\"params\": [\"{market.Symbol.ToLower()}@depth@100ms\"], \"id\": {ID} }}")
                                ), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                    ID++;
                } else
                {
                    Exchange.SubscriptionQueue.Enqueue(market);
                }
                
                await Task.Delay(250);
            }

            await Task.Run(() => Exchange.Clients.Add(Client));
        }
    }
}