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
        public string exchangeName { get; set; }

        public HashSet<Orderbook> tradedMarkets = new HashSet<Orderbook>(); //all markets traded on the exchange

        public ConcurrentDictionary<string, Orderbook> OfficialOrderbooks = new ConcurrentDictionary<string, Orderbook>(); //official orderbooks...tradedMarkets is likely to be merged into this

        public ConcurrentQueue<Triangle> TrianglesToRecalculate = new ConcurrentQueue<Triangle>();
        
        public ConcurrentDictionary<string, Triangle> Triangles = new ConcurrentDictionary<string, Triangle>(); //rename this member to describe what it's for

        public HashSet<Orderbook> triarbEligibleMarkets = new HashSet<Orderbook>(); //all markets that interact with a triarb opportunity

        public ConcurrentDictionary<string, List<Triangle>> triarbMarketMapping = new ConcurrentDictionary<string, List<Triangle>>(); //maps market IDs to triangles

        public double impactedTriangleCounter = 0;

        public double redundantTriangleCounter = 0;

        public double allOrderBookCounter = 0;

        public double InsideLayerCounter = 0; 

        public double OutsideLayerCounter = 0;

        public double PositivePriceChangeCounter = 0; 

        public double NegativePriceChangeCounter = 0;

        public int uniqueTriangleCount = 0;

        public ConcurrentDictionary<string, int> ProfitableSymbolMapping = new ConcurrentDictionary<string, int>();

        public ConcurrentDictionary<string, DateTime> TriangleRefreshTimes = new ConcurrentDictionary<string, DateTime>();

        public ConcurrentQueue<Triangle> RecalculatedTriangles = new ConcurrentQueue<Triangle>();

        public string tickerRESTAPI { get; set; } //the URL of rest API call for the exchange

        private static ILoggerFactory _factory = new NullLoggerFactory();

        public Exchange(string name)
        {
            exchangeName = name;
            tradedMarkets = parseMarkets(TriangleCollector.restAPIs.tickers[exchangeName]); //pull the REST API response from the restAPI object which stores the restAPI responses for each exchange, indexed by exchange name.
            mapOpportunities();
            Console.WriteLine($"there are {tradedMarkets.Count} markets traded on {exchangeName}. Of these markets, {triarbEligibleMarkets.Count} markets interact to form {uniqueTriangleCount} triangular arbitrage opportunities");
        }

        public void mapOpportunities() //this will be its own class
        {
            foreach (var firstMarket in tradedMarkets)
            {
                if (firstMarket.quoteCurrency == "BTC" || firstMarket.baseCurrency == "BTC")
                {
                    if (firstMarket.quoteCurrency == "BTC")
                    {
                        var firstDirection = "Buy";
                        foreach (var secondMarket in tradedMarkets)
                        {
                            if (secondMarket.baseCurrency == firstMarket.baseCurrency || secondMarket.quoteCurrency == firstMarket.baseCurrency && secondMarket.symbol != firstMarket.symbol)
                            {
                                if (secondMarket.baseCurrency == firstMarket.baseCurrency)
                                {
                                    var secondDirection = "Sell";
                                    foreach (var thirdMarket in tradedMarkets)
                                    {
                                        if (thirdMarket.quoteCurrency == "BTC" && thirdMarket.baseCurrency == secondMarket.quoteCurrency)
                                        {
                                            var thirdDirection = "Sell";
                                            mapHelper(firstDirection, secondDirection, thirdDirection, firstMarket, secondMarket, thirdMarket);
                                        }
                                    }
                                }
                                else
                                {
                                    var secondDirection = "Buy";
                                    foreach (var thirdMarket in tradedMarkets)
                                    {
                                        if (thirdMarket.quoteCurrency == "BTC" && thirdMarket.baseCurrency == secondMarket.baseCurrency)
                                        {
                                            var thirdDirection = "Sell";
                                            mapHelper(firstDirection, secondDirection, thirdDirection, firstMarket, secondMarket, thirdMarket);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        var firstDirection = "Sell";
                        foreach (var secondMarket in tradedMarkets)
                        {
                            if (secondMarket.quoteCurrency == firstMarket.quoteCurrency && secondMarket.symbol != firstMarket.symbol)
                            {
                                var secondDirection = "Buy";
                                foreach (var thirdMarket in tradedMarkets)
                                {
                                    if (thirdMarket.quoteCurrency == "BTC" && thirdMarket.baseCurrency == secondMarket.baseCurrency)
                                    {
                                        var thirdDirection = "Sell";
                                        mapHelper(firstDirection, secondDirection, thirdDirection, firstMarket, secondMarket, thirdMarket);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void mapHelper(string firstDirection, string secondDirection, string thirdDirection, Orderbook firstMarket, Orderbook secondMarket, Orderbook thirdMarket)
        {
            var direction = (Triangle.Directions)Enum.Parse(typeof(Triangle.Directions), $"{firstDirection}{secondDirection}{thirdDirection}");
            var newTriangle = new Triangle(firstMarket.symbol, secondMarket.symbol, thirdMarket.symbol, direction, _factory.CreateLogger<Triangle>(), this);
            //Console.WriteLine($"{exchangeName}: {firstDirection} {firstMarket.symbol}, {secondDirection} {secondMarket.symbol}, {thirdDirection} {thirdMarket.symbol}");
            uniqueTriangleCount++;
            triarbEligibleMarkets.Add(firstMarket);
            triarbEligibleMarkets.Add(secondMarket);
            triarbEligibleMarkets.Add(thirdMarket);
            OfficialOrderbooks.AddOrUpdate(firstMarket.symbol, firstMarket, (key, oldValue) => oldValue = firstMarket);
            OfficialOrderbooks.AddOrUpdate(secondMarket.symbol, secondMarket, (key, oldValue) => oldValue = secondMarket);
            OfficialOrderbooks.AddOrUpdate(thirdMarket.symbol, thirdMarket, (key, oldValue) => oldValue = thirdMarket);
            foreach (var trade in new List<string> { firstMarket.symbol, secondMarket.symbol, thirdMarket.symbol })
            {
                triarbMarketMapping.AddOrUpdate(trade, new List<Triangle>() { newTriangle }, (key, triangleList) =>
                {
                    if (key == trade)
                    {
                        triangleList.Add(newTriangle);
                    }
                    return triangleList;
                });
            }
        }
    

         public HashSet<Orderbook> parseMarkets(JsonElement.ArrayEnumerator symbols) // each exchange uses different JSON properties to describe the attributes of a market.
         {
            var output = new HashSet<Orderbook>();
            if (exchangeName == "hitbtc") //https://api.hitbtc.com/#symbols
            {
                foreach (var responseItem in symbols)
                {
                    var market = new Orderbook();
                    market.symbol = responseItem.GetProperty("id").ToString();
                    market.baseCurrency = responseItem.GetProperty("baseCurrency").ToString();
                    market.quoteCurrency = responseItem.GetProperty("quoteCurrency").ToString();
                    market.exchange = this;
                    output.Add(market);
                }
            }
            else if (exchangeName == "binance") //https://binance-docs.github.io/apidocs/spot/en/#exchange-information
            {
                foreach (var responseItem in symbols)
                {
                    var market = new Orderbook();
                    market.symbol = responseItem.GetProperty("symbol").ToString();
                    market.baseCurrency = responseItem.GetProperty("baseAsset").ToString();
                    market.quoteCurrency = responseItem.GetProperty("quoteAsset").ToString();
                    market.exchange = this;
                    output.Add(market);
                }
            }
            else if (exchangeName == "bittrex") //https://bittrex.github.io/api/v3
            {
                foreach (var responseItem in symbols)
                {
                    var market = new Orderbook();
                    market.symbol = responseItem.GetProperty("symbol").ToString();
                    market.baseCurrency = responseItem.GetProperty("baseCurrencySymbol").ToString();
                    market.quoteCurrency = responseItem.GetProperty("quoteCurrencySymbol").ToString();
                    market.exchange = this;
                    output.Add(market);
                }
            }
            else if (exchangeName == "huobi") //https://huobiapi.github.io/docs/spot/v1/en/#get-all-supported-trading-symbol
            {
                foreach (var responseItem in symbols)
                {
                    var market = new Orderbook();
                    market.symbol = responseItem.GetProperty("symbol").ToString().ToUpper();
                    market.baseCurrency = responseItem.GetProperty("base-currency").ToString().ToUpper();
                    market.quoteCurrency = responseItem.GetProperty("quote-currency").ToString().ToUpper();
                    market.exchange = this;
                    output.Add(market);
                }
            }
            return (output);
         }
            

    }
}
        

    

