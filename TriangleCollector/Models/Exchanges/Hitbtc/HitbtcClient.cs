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
        public string SymbolsRestApi { get; set; } = "https://api.hitbtc.com/api/2/public/symbol"; //REST API call to get all of the traded markets
        public string PricesRestApi { get; set; } = "https://api.hitbtc.com/api/2/public/orderbook/?limit=1"; //REST API call to get all of the best prices for all markets
        public string SocketClientApi { get; set; } = "wss://api.hitbtc.com/api/2/ws";
        public JsonElement.ArrayEnumerator Tickers { get; set; }
        public List<IClientWebSocket> Clients { get; set; } = new List<IClientWebSocket>();
        public IClientWebSocket Client { get; set; }
        public int MaxMarketsPerClient { get; } = 40;
        private HttpClient HttpClient = new HttpClient();


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
        public HashSet<IOrderbook> GetMarketsViaRestApi() //Poll the REST API for the exchange to get all traded markets and their best price/sizes
        {
            var output = new HashSet<IOrderbook>();
            var symbols = JsonDocument.ParseAsync(HttpClient.GetStreamAsync(SymbolsRestApi).Result).Result.RootElement.EnumerateArray();
            foreach (var responseItem in symbols)
            {
                var market = new HitbtcOrderbook();
                market.Symbol = responseItem.GetProperty("id").ToString();
                market.BaseCurrency = responseItem.GetProperty("baseCurrency").ToString();
                market.QuoteCurrency = responseItem.GetProperty("quoteCurrency").ToString();
                market.Exchange = Exchange;
                output.Add(market);
            }
            var tickerPrices = JsonDocument.ParseAsync(HttpClient.GetStreamAsync(PricesRestApi).Result).Result.RootElement;
            foreach (var market in output)
            {
                try
                {
                    var tickerJSON = tickerPrices.GetProperty(market.Symbol);
                    var tickerAsks = tickerJSON.GetProperty("ask").EnumerateArray();
                    var tickerBids = tickerJSON.GetProperty("bid").EnumerateArray();
                    foreach (var ask in tickerAsks)
                    {
                        var askPrice = Decimal.Parse(ask.GetProperty("price").ToString());
                        var askSize = Decimal.Parse(ask.GetProperty("size").ToString());
                        market.OfficialAsks.TryAdd(askPrice, askSize);
                    }
                    foreach (var bid in tickerBids)
                    {
                        var bidPrice = Decimal.Parse(bid.GetProperty("price").ToString());
                        var bidSize = Decimal.Parse(bid.GetProperty("size").ToString());
                        market.OfficialBids.TryAdd(bidPrice, bidSize);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    continue;
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

            client.Options.KeepAliveInterval = new TimeSpan(0, 0, 5);
            await client.ConnectAsync(new Uri(SocketClientApi), CancellationToken.None);
            adapter.TimeStarted = DateTime.UtcNow;
            Client = adapter;
            return adapter;
        }

        public async Task Subscribe(List<IOrderbook> Markets)
        {
            foreach (var market in Markets)
            {
                if(Client.State == WebSocketState.Open) //given the amount of time it takes to complete this for loop, a client could be aborted in process.
                {
                    await Client.SendAsync(
                    new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{{\"method\": \"subscribeOrderbook\",\"params\": {{ \"symbol\": \"{market.Symbol}\" }} }}")),
                    WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                    Exchange.SubscribedMarkets.Add(market);
                } else //client was aborted prior to completing the for loop
                {
                    Exchange.SubscriptionQueue.Enqueue(market); //add these markets back to the queue
                }
                await Task.Delay(250); //encountered '429' responses from hitbtc for exceeding the rate limit, which appears to be 100 requests per second
            }

            await Task.Run(() => Exchange.Clients.Add(Client));
        }
    }
}
