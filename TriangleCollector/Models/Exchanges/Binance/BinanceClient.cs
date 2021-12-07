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
        public string PublicMarketDataSocketClientUrl { get; set; } = "wss://stream.binance.com:9443/ws";
        public JsonElement.ArrayEnumerator Tickers { get; set; }
        public IClientWebSocket PublicClient { get; set; }
        public int MaxMarketsPerClient { get; } = 20;

        public string PrivateAccountDataSocketClientUrl { get; set; }

        public int ID = 1;

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
                    if(output.Where(m => m.Symbol == symbol).Count() > 0)
                    {
                        output.Where(m => m.Symbol == symbol).First().OfficialBids.TryAdd(bidPrice, bidSize);
                        output.Where(m => m.Symbol == symbol).First().OfficialAsks.TryAdd(askPrice, askSize);
                    }
                }
            }
            output = output.Where(m => m.OfficialAsks.Count > 0 && m.OfficialBids.Count > 0).ToHashSet();
            return output;
        }

        public async Task<WebSocketAdapter> CreatePublicExchangeClientAsync()
        {
            var client = new ClientWebSocket();
            var factory = new LoggerFactory();
            var adapter = new WebSocketAdapter(factory.CreateLogger<WebSocketAdapter>(), client);

            client.Options.KeepAliveInterval = new TimeSpan(0, 0, 5);
            await client.ConnectAsync(new Uri(PublicMarketDataSocketClientUrl), CancellationToken.None);
            adapter.TimeStarted = DateTime.UtcNow;
            adapter.Markets = new List<IOrderbook>();
            PublicClient = adapter;
            Exchange.ActiveClients.Add(PublicClient);
            await Task.Delay(250); // clients with zero subscriptions are being aborted; give 1/4 second to ensure connection is complete
            return adapter;
        }

        public async Task SubscribeViaAggregate()
        {
            if (PublicClient.State == WebSocketState.Open)
            {
                await PublicClient.SendAsync(new ArraySegment<byte>(
                        Encoding.ASCII.GetBytes($"{{\"method\": \"SUBSCRIBE\",\"params\": [\"!bookTicker\"], \"id\": {ID} }}")
                        ), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                ID++;
                Exchange.AggregateStreamOpen = true;
            } 
            await Task.Delay(500);
        }
        public async Task SubscribeViaQueue(IOrderbook market)
        {
            if (PublicClient.State == WebSocketState.Open)
            {
                await PublicClient.SendAsync(new ArraySegment<byte>(
                        Encoding.ASCII.GetBytes($"{{\"method\": \"SUBSCRIBE\",\"params\": [\"{market.Symbol.ToLower()}@bookTicker\"], \"id\": {ID} }}")
                        ), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                ID++;
                PublicClient.Markets.Add(market);
            }
            await Task.Delay(250);
        }
    }
}