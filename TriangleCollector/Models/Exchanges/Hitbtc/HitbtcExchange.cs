using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TriangleCollector.Models.Interfaces;

namespace TriangleCollector.Models.Exchanges.Hitbtc
{
    public class HitbtcExchange : IExchange
    {
        public string ExchangeName { get; }

        public IExchangeClient ExchangeClient { get; } = new HitbtcClient();

        public List<IClientWebSocket> Clients { get; } = new List<IClientWebSocket>();

        public Type OrderbookType { get; } = typeof(HitbtcOrderbook);

        public HashSet<IOrderbook> TradedMarkets { get; } = new HashSet<IOrderbook>();

        public ConcurrentDictionary<string, IOrderbook> OfficialOrderbooks { get; } = new ConcurrentDictionary<string, IOrderbook>();

        public ConcurrentQueue<Triangle> TrianglesToRecalculate { get; } = new ConcurrentQueue<Triangle>();

        public ConcurrentDictionary<string, Triangle> Triangles { get; } = new ConcurrentDictionary<string, Triangle>();

        public HashSet<IOrderbook> TriarbEligibleMarkets { get; } = new HashSet<IOrderbook>();

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

        public HitbtcExchange(string name)
        {
            ExchangeName = name;
            ExchangeClient.GetTickers();
            ExchangeClient.Exchange = this;
            TradedMarkets = ParseMarkets(ExchangeClient.Tickers); //pull the REST API response from the restAPI object which stores the restAPI responses for each exchange, indexed by exchange name.
            MapOpportunities();
            //Console.WriteLine($"there are {TradedMarkets.Count} markets traded on {ExchangeName}. Of these markets, {TriarbEligibleMarkets.Count} markets interact to form {UniqueTriangleCount} triangular arbitrage opportunities");
        }

        public HashSet<IOrderbook> ParseMarkets(JsonElement.ArrayEnumerator symbols)
        {
            var output = new HashSet<IOrderbook>();
            foreach (var responseItem in symbols)
            {
                var market = new HitbtcOrderbook();
                market.Symbol = responseItem.GetProperty("id").ToString();
                market.BaseCurrency = responseItem.GetProperty("baseCurrency").ToString();
                market.QuoteCurrency = responseItem.GetProperty("quoteCurrency").ToString();
                market.Exchange = this;
                output.Add(market);
            }
            //if (ExchangeName == "hitbtc") //https://api.hitbtc.com/#symbols
            //{
            //    foreach (var responseItem in symbols)
            //    {
            //        var market = new BinanceOrderbook();
            //        market.Symbol = responseItem.GetProperty("id").ToString();
            //        market.BaseCurrency = responseItem.GetProperty("baseCurrency").ToString();
            //        market.QuoteCurrency = responseItem.GetProperty("quoteCurrency").ToString();
            //        market.Exchange = this;
            //        output.Add(market);
            //    }
            //}
            //else if (ExchangeName == "binance") //https://binance-docs.github.io/apidocs/spot/en/#exchange-information
            //{
            //    foreach (var responseItem in symbols)
            //    {
            //        if (responseItem.GetProperty("status").ToString() == "TRADING") //only include markets that are actively traded (as opposed to delisted or inactive)
            //        {
            //            var market = new BinanceOrderbook();
            //            market.Symbol = responseItem.GetProperty("symbol").ToString();
            //            market.BaseCurrency = responseItem.GetProperty("baseAsset").ToString();
            //            market.QuoteCurrency = responseItem.GetProperty("quoteAsset").ToString();
            //            market.Exchange = this;
            //            output.Add(market);
            //        }
            //    }
            //}
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
            //else if (ExchangeName == "huobi") //https://huobiapi.github.io/docs/spot/v1/en/#get-all-supported-trading-symbol
            //{
            //    foreach (var responseItem in symbols)
            //    {
            //        if (responseItem.GetProperty("state").ToString() == "online") //huobi includes delisted/disabled markets in their response, only consider active/onlien markets.
            //        {
            //            var market = new BinanceOrderbook();
            //            market.Symbol = responseItem.GetProperty("symbol").ToString().ToUpper();
            //            market.BaseCurrency = responseItem.GetProperty("base-currency").ToString().ToUpper();
            //            market.QuoteCurrency = responseItem.GetProperty("quote-currency").ToString().ToUpper();
            //            market.Exchange = this;
            //            output.Add(market);
            //        }
            //    }
            //}
            return (output);
        }

        public void MapHelper(string firstDirection, string secondDirection, string thirdDirection, IOrderbook firstMarket, IOrderbook secondMarket, IOrderbook thirdMarket)
        {
            var direction = (Triangle.Directions)Enum.Parse(typeof(Triangle.Directions), $"{firstDirection}{secondDirection}{thirdDirection}");
            var newTriangle = new Triangle(firstMarket.Symbol, secondMarket.Symbol, thirdMarket.Symbol, direction, this);
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

        public void MapOpportunities()
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
    }
}

