using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TriangleCollector.Services;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Services;



namespace TriangleCollector.Models.Exchange_Models
{
    public class Exchange
    {
        public string ExchangeName { get; set; }

        public HashSet<Orderbook> TradedMarkets = new HashSet<Orderbook>(); //all markets traded on the exchange

        public ConcurrentDictionary<string, Orderbook> OfficialOrderbooks = new ConcurrentDictionary<string, Orderbook>(); //official orderbooks...tradedMarkets is likely to be merged into this

        public ConcurrentQueue<Triangle> TrianglesToRecalculate = new ConcurrentQueue<Triangle>();
        
        public ConcurrentDictionary<string, Triangle> Triangles = new ConcurrentDictionary<string, Triangle>(); //rename this member to describe what it's for

        public HashSet<Orderbook> TriarbEligibleMarkets = new HashSet<Orderbook>(); //all markets that interact with a triarb opportunity

        public ConcurrentDictionary<string, List<Triangle>> TriarbMarketMapping = new ConcurrentDictionary<string, List<Triangle>>(); //maps market IDs to triangles

        public double ImpactedTriangleCounter = 0;

        public double RedundantTriangleCounter = 0;

        public double AllOrderBookCounter = 0;

        public double InsideLayerCounter = 0; 

        public double OutsideLayerCounter = 0;

        public double PositivePriceChangeCounter = 0; 

        public double NegativePriceChangeCounter = 0;

        public int UniqueTriangleCount = 0;

        public ConcurrentDictionary<string, int> ProfitableSymbolMapping = new ConcurrentDictionary<string, int>();

        public ConcurrentDictionary<string, DateTime> TriangleRefreshTimes = new ConcurrentDictionary<string, DateTime>();

        public ConcurrentQueue<Triangle> RecalculatedTriangles = new ConcurrentQueue<Triangle>();

        private static ILoggerFactory _factory = new NullLoggerFactory();

        public Exchange(string name)
        {
            ExchangeName = name;
            TradedMarkets = ParseMarkets(TriangleCollector.RestAPIs.Tickers[ExchangeName]); //pull the REST API response from the restAPI object which stores the restAPI responses for each exchange, indexed by exchange name.
            MapOpportunities();
            Console.WriteLine($"there are {TradedMarkets.Count} markets traded on {ExchangeName}. Of these markets, {TriarbEligibleMarkets.Count} markets interact to form {UniqueTriangleCount} triangular arbitrage opportunities");
        }

        public void MapOpportunities() //this will be its own class
        {
            foreach (var firstMarket in TradedMarkets)
            {
                if (firstMarket.QuoteCurrency == "BTC" || firstMarket.BaseCurrency == "BTC")
                {
                    if (firstMarket.QuoteCurrency == "BTC")
                    {
                        var firstDirection = "Buy";
                        foreach (var secondMarket in TradedMarkets)
                        {
                            if (secondMarket.BaseCurrency == firstMarket.BaseCurrency || secondMarket.QuoteCurrency == firstMarket.BaseCurrency && secondMarket.Symbol != firstMarket.Symbol)
                            {
                                if (secondMarket.BaseCurrency == firstMarket.BaseCurrency)
                                {
                                    var secondDirection = "Sell";
                                    foreach (var thirdMarket in TradedMarkets)
                                    {
                                        if (thirdMarket.QuoteCurrency == "BTC" && thirdMarket.BaseCurrency == secondMarket.QuoteCurrency)
                                        {
                                            var thirdDirection = "Sell";
                                            MapHelper(firstDirection, secondDirection, thirdDirection, firstMarket, secondMarket, thirdMarket);
                                        }
                                    }
                                }
                                else
                                {
                                    var secondDirection = "Buy";
                                    foreach (var thirdMarket in TradedMarkets)
                                    {
                                        if (thirdMarket.QuoteCurrency == "BTC" && thirdMarket.BaseCurrency == secondMarket.BaseCurrency)
                                        {
                                            var thirdDirection = "Sell";
                                            MapHelper(firstDirection, secondDirection, thirdDirection, firstMarket, secondMarket, thirdMarket);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        var firstDirection = "Sell";
                        foreach (var secondMarket in TradedMarkets)
                        {
                            if (secondMarket.QuoteCurrency == firstMarket.QuoteCurrency && secondMarket.Symbol != firstMarket.Symbol)
                            {
                                var secondDirection = "Buy";
                                foreach (var thirdMarket in TradedMarkets)
                                {
                                    if (thirdMarket.QuoteCurrency == "BTC" && thirdMarket.BaseCurrency == secondMarket.BaseCurrency)
                                    {
                                        var thirdDirection = "Sell";
                                        MapHelper(firstDirection, secondDirection, thirdDirection, firstMarket, secondMarket, thirdMarket);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void MapHelper(string firstDirection, string secondDirection, string thirdDirection, Orderbook firstMarket, Orderbook secondMarket, Orderbook thirdMarket)
        {
            var direction = (Triangle.Directions)Enum.Parse(typeof(Triangle.Directions), $"{firstDirection}{secondDirection}{thirdDirection}");
            var newTriangle = new Triangle(firstMarket.Symbol, secondMarket.Symbol, thirdMarket.Symbol, direction, _factory.CreateLogger<Triangle>(), this);
            //Console.WriteLine($"{exchangeName}: {firstDirection} {firstMarket.symbol}, {secondDirection} {secondMarket.symbol}, {thirdDirection} {thirdMarket.symbol}");
            UniqueTriangleCount++;
            TriarbEligibleMarkets.Add(firstMarket);
            TriarbEligibleMarkets.Add(secondMarket);
            TriarbEligibleMarkets.Add(thirdMarket);
            OfficialOrderbooks.AddOrUpdate(firstMarket.Symbol, firstMarket, (key, oldValue) => oldValue = firstMarket);
            OfficialOrderbooks.AddOrUpdate(secondMarket.Symbol, secondMarket, (key, oldValue) => oldValue = secondMarket);
            OfficialOrderbooks.AddOrUpdate(thirdMarket.Symbol, thirdMarket, (key, oldValue) => oldValue = thirdMarket);
            foreach (var trade in new List<string> { firstMarket.Symbol, secondMarket.Symbol, thirdMarket.Symbol })
            {
                TriarbMarketMapping.AddOrUpdate(trade, new List<Triangle>() { newTriangle }, (key, triangleList) =>
                {
                    if (key == trade)
                    {
                        triangleList.Add(newTriangle);
                    }
                    return triangleList;
                });
            }
        }
    

         public HashSet<Orderbook> ParseMarkets(JsonElement.ArrayEnumerator symbols) // each exchange uses different JSON properties to describe the attributes of a market.
         {
            var output = new HashSet<Orderbook>();
            if (ExchangeName == "hitbtc") //https://api.hitbtc.com/#symbols
            {
                foreach (var responseItem in symbols)
                {
                    var market = new Orderbook();
                    market.Symbol = responseItem.GetProperty("id").ToString();
                    market.BaseCurrency = responseItem.GetProperty("baseCurrency").ToString();
                    market.QuoteCurrency = responseItem.GetProperty("quoteCurrency").ToString();
                    market.Exchange = this;
                    output.Add(market);
                }
            }
            else if (ExchangeName == "binance") //https://binance-docs.github.io/apidocs/spot/en/#exchange-information
            {
                foreach (var responseItem in symbols)
                {
                    if(responseItem.GetProperty("status").ToString() == "TRADING") //only include markets that are actively traded (as opposed to delisted or inactive)
                    {
                        var market = new Orderbook();
                        market.Symbol = responseItem.GetProperty("symbol").ToString();
                        market.BaseCurrency = responseItem.GetProperty("baseAsset").ToString();
                        market.QuoteCurrency = responseItem.GetProperty("quoteAsset").ToString();
                        market.Exchange = this;
                        output.Add(market);
                    }
                }
            }
            else if (ExchangeName == "bittrex") //https://bittrex.github.io/api/v3
            {
                foreach (var responseItem in symbols)
                {
                    var market = new Orderbook();
                    market.Symbol = responseItem.GetProperty("symbol").ToString();
                    market.BaseCurrency = responseItem.GetProperty("baseCurrencySymbol").ToString();
                    market.QuoteCurrency = responseItem.GetProperty("quoteCurrencySymbol").ToString();
                    market.Exchange = this;
                    output.Add(market);
                }
            }
            else if (ExchangeName == "huobi") //https://huobiapi.github.io/docs/spot/v1/en/#get-all-supported-trading-symbol
            {
                foreach (var responseItem in symbols)
                {
                    if(responseItem.GetProperty("state").ToString() =="online") //huobi includes delisted/disabled markets in their response, only consider active/onlien markets.
                    {
                        var market = new Orderbook();
                        market.Symbol = responseItem.GetProperty("symbol").ToString().ToUpper();
                        market.BaseCurrency = responseItem.GetProperty("base-currency").ToString().ToUpper();
                        market.QuoteCurrency = responseItem.GetProperty("quote-currency").ToString().ToUpper();
                        market.Exchange = this;
                        output.Add(market);
                    }
                }
            }
            return (output);
         }
            

    }
}
        

    

