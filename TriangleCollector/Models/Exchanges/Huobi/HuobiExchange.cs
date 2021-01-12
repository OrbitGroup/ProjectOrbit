using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TriangleCollector.Models.Interfaces;
using TriangleCollector.Services;

namespace TriangleCollector.Models.Exchanges.Huobi
{
    public class HuobiExchange : IExchange
    {
        public string ExchangeName { get; }

        public IExchangeClient ExchangeClient { get; } = new HuobiClient();

        public List<IClientWebSocket> Clients { get; } = new List<IClientWebSocket>();

        public Type OrderbookType { get; } = typeof(HuobiOrderbook);

        public HashSet<IOrderbook> TradedMarkets { get; set;  } = new HashSet<IOrderbook>();

        public ConcurrentDictionary<string, IOrderbook> OfficialOrderbooks { get; } = new ConcurrentDictionary<string, IOrderbook>();

        public ConcurrentQueue<Triangle> TrianglesToRecalculate { get; } = new ConcurrentQueue<Triangle>();

        public ConcurrentDictionary<string, Triangle> Triangles { get; } = new ConcurrentDictionary<string, Triangle>();

        public HashSet<IOrderbook> TriarbEligibleMarkets { get; } = new HashSet<IOrderbook>();
        public Queue<IOrderbook> SubscriptionQueue { get; set; } = new Queue<IOrderbook>();

        public ConcurrentDictionary<string, List<Triangle>> TriarbMarketMapping { get; } = new ConcurrentDictionary<string, List<Triangle>>();

        public double ImpactedTriangleCounter { get; set; } = 0;

        public double RedundantTriangleCounter { get; set; } = 0;

        public double AllOrderBookCounter { get; set; } = 0;

        public double InsideLayerCounter { get; set; } = 0;

        public double OutsideLayerCounter { get; set; } = 0;

        public double PositivePriceChangeCounter { get; set; } = 0;

        public double NegativePriceChangeCounter { get; set; } = 0;

        public int UniqueTriangleCount { get; set; } = 0;

        public ConcurrentDictionary<string, int> ProfitableSymbolMapping { get; } = new ConcurrentDictionary<string, int>();

        public ConcurrentDictionary<string, DateTime> TriangleRefreshTimes { get; } = new ConcurrentDictionary<string, DateTime>();

        public ConcurrentQueue<Triangle> RecalculatedTriangles { get; } = new ConcurrentQueue<Triangle>();

        public HuobiExchange(string name)
        {
            ExchangeName = name;
            ExchangeClient.GetTickers();
            ExchangeClient.Exchange = this;
            TradedMarkets = ParseMarkets(ExchangeClient.Tickers); //pull the REST API response from the restAPI object which stores the restAPI responses for each exchange, indexed by exchange name.
            MarketMapper.MapOpportunities(this);
            //Console.WriteLine($"there are {TradedMarkets.Count} markets traded on {ExchangeName}. Of these markets, {TriarbEligibleMarkets.Count} markets interact to form {UniqueTriangleCount} triangular arbitrage opportunities");
        }

        public HashSet<IOrderbook> ParseMarkets(JsonElement.ArrayEnumerator symbols)
        {
            var output = new HashSet<IOrderbook>();
            foreach (var responseItem in symbols)
            {
                if (responseItem.GetProperty("state").ToString() == "online") //huobi includes delisted/disabled markets in their response, only consider active/onlien markets.
                {
                    var market = new HuobiOrderbook();
                    market.Symbol = responseItem.GetProperty("symbol").ToString().ToUpper();
                    market.BaseCurrency = responseItem.GetProperty("base-currency").ToString().ToUpper();
                    market.QuoteCurrency = responseItem.GetProperty("quote-currency").ToString().ToUpper();
                    market.Exchange = this;
                    output.Add(market);
                }
            }
            //else if (ExchangeName == "bittrex") //https://bittrex.github.io/api/v3
            //{
            //    foreach (var responseItem in symbols)
            //    {
            //        var market = new BinanceOrderbook();
            //        market.Symbol = responseItem.GetProperty("symbol").ToString();
            //        market.BaseCurrency = responseItem.GetProperty("baseCurrencySymbol").ToString();
            //        market.QuoteCurrency = responseItem.GetProperty("quoteCurrencySymbol").ToString();
            //        market.Exchange = this;
            //        output.Add(market);
            //    }
            //}
            return (output);
        }
    }
}

