using System;
using System.Collections.Generic;
using System.Text;
using TriangleCollector.Models.Interfaces;
using TriangleCollector.Models;

namespace TriangleCollector.Services
{
    public class MarketMapper
    {
        private static int UniqueTriangleCount = 0;
        private static HashSet<IOrderbook> TriarbEligibleMarkets = new HashSet<IOrderbook>();

        public static void MapOpportunities(IExchange Exchange)
        {
            foreach (var firstMarket in Exchange.TradedMarkets)
            {
                if (firstMarket.QuoteCurrency == "BTC" || firstMarket.BaseCurrency == "BTC")
                {
                    if (firstMarket.QuoteCurrency == "BTC")
                    {
                        var firstDirection = "Buy";
                        foreach (var secondMarket in Exchange.TradedMarkets)
                        {
                            if (secondMarket.BaseCurrency == firstMarket.BaseCurrency || secondMarket.QuoteCurrency == firstMarket.BaseCurrency && secondMarket.Symbol != firstMarket.Symbol)
                            {
                                if (secondMarket.BaseCurrency == firstMarket.BaseCurrency)
                                {
                                    var secondDirection = "Sell";
                                    foreach (var thirdMarket in Exchange.TradedMarkets)
                                    {
                                        if (thirdMarket.QuoteCurrency == "BTC" && thirdMarket.BaseCurrency == secondMarket.QuoteCurrency)
                                        {
                                            var thirdDirection = "Sell";
                                            MapHelper(firstDirection, secondDirection, thirdDirection, firstMarket, secondMarket, thirdMarket, Exchange);
                                        }
                                    }
                                }
                                else
                                {
                                    var secondDirection = "Buy";
                                    foreach (var thirdMarket in Exchange.TradedMarkets)
                                    {
                                        if (thirdMarket.QuoteCurrency == "BTC" && thirdMarket.BaseCurrency == secondMarket.BaseCurrency)
                                        {
                                            var thirdDirection = "Sell";
                                            MapHelper(firstDirection, secondDirection, thirdDirection, firstMarket, secondMarket, thirdMarket, Exchange);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        var firstDirection = "Sell";
                        foreach (var secondMarket in Exchange.TradedMarkets)
                        {
                            if (secondMarket.QuoteCurrency == firstMarket.QuoteCurrency && secondMarket.Symbol != firstMarket.Symbol)
                            {
                                var secondDirection = "Buy";
                                foreach (var thirdMarket in Exchange.TradedMarkets)
                                {
                                    if (thirdMarket.QuoteCurrency == "BTC" && thirdMarket.BaseCurrency == secondMarket.BaseCurrency)
                                    {
                                        var thirdDirection = "Sell";
                                        MapHelper(firstDirection, secondDirection, thirdDirection, firstMarket, secondMarket, thirdMarket, Exchange);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            Exchange.UniqueTriangleCount = UniqueTriangleCount;
            Exchange.TriarbEligibleMarkets = TriarbEligibleMarkets;
        }
        public static void MapHelper(string firstDirection, string secondDirection, string thirdDirection, IOrderbook firstMarket, IOrderbook secondMarket, IOrderbook thirdMarket, IExchange Exchange)
        {
            var direction = (Triangle.Directions)Enum.Parse(typeof(Triangle.Directions), $"{firstDirection}{secondDirection}{thirdDirection}");
            var newTriangle = new Triangle(firstMarket.Symbol, secondMarket.Symbol, thirdMarket.Symbol, direction, Exchange);
            //Console.WriteLine($"{exchangeName}: {firstDirection} {firstMarket.symbol}, {secondDirection} {secondMarket.symbol}, {thirdDirection} {thirdMarket.symbol}");
            UniqueTriangleCount++;
            TriarbEligibleMarkets.Add(firstMarket);
            TriarbEligibleMarkets.Add(secondMarket);
            TriarbEligibleMarkets.Add(thirdMarket);
            Exchange.OfficialOrderbooks.TryAdd(firstMarket.Symbol, firstMarket);
            Exchange.OfficialOrderbooks.TryAdd(secondMarket.Symbol, secondMarket);
            Exchange.OfficialOrderbooks.TryAdd(thirdMarket.Symbol, thirdMarket);
            foreach (var marketName in new List<string> { firstMarket.Symbol, secondMarket.Symbol, thirdMarket.Symbol })
            {
                if(!Exchange.TriarbMarketMapping.Keys.Contains(marketName))
                {
                    Exchange.TriarbMarketMapping.AddOrUpdate(marketName, new List<Triangle>() { newTriangle }, (key, triangleList) =>
                    {
                        if (key == marketName && !triangleList.Contains(newTriangle))
                        {
                            triangleList.Add(newTriangle);
                        }
                        return triangleList;
                    });
                } else
                {
                    Exchange.TriarbMarketMapping.TryGetValue(marketName, out var triangles);
                    if(!triangles.Contains(newTriangle))
                    {
                        Exchange.TriarbMarketMapping.AddOrUpdate(marketName, new List<Triangle>() { newTriangle }, (key, triangleList) =>
                        {
                            if (key == marketName && !triangleList.Contains(newTriangle))
                            {
                                triangleList.Add(newTriangle);
                            }
                            return triangleList;
                        });
                    }
                }
            }
        }
    }
}
