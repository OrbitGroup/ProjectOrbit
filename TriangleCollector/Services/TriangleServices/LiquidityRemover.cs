using System.Collections.Generic;
using System.Linq;
using static TriangleCollector.Models.Triangle;

namespace TriangleCollector.Models
{
    public static class LiquidityRemover
    {
        public static void RemoveLiquidity(KeyValuePair<Bottlenecks, decimal> bottleneck, Triangle triangle)
        {
            //use min/max functions once to define the lowest layers up front as opposed to calling the min/max methods repeatedly (often 7/8 times per layer) throughout the process.
            var thirdSymbolHighestBidKey = triangle.ThirdOrderBook.Keys.Max();
            var firstSymbolLowestAskKey = triangle.FirstOrderBook.Keys.Min();
            var secondSymbolLowestAskKey = triangle.SecondOrderBook.Keys.Min();
            var firstSymbolHighestBidKey = triangle.FirstOrderBook.Keys.Max();

            if (triangle.Direction == Directions.BuyBuySell)
            {
                if (bottleneck.Key == Bottlenecks.FirstTrade)
                {
                    triangle.FirstOrderBook.Remove(firstSymbolLowestAskKey, out var _);

                    triangle.SecondOrderBook[secondSymbolLowestAskKey] = triangle.SecondOrderBook[secondSymbolLowestAskKey] - bottleneck.Value / triangle.FirstOrderBookVolumeConverter.Value / secondSymbolLowestAskKey; //second trade is quoted in alt terms, so convert using first orderbook.
                    triangle.ThirdOrderBook[thirdSymbolHighestBidKey] = triangle.ThirdOrderBook[thirdSymbolHighestBidKey] - bottleneck.Value / thirdSymbolHighestBidKey; //last trade must be quoted in BTC terms
                }
                else if (bottleneck.Key == Bottlenecks.SecondTrade)
                {
                    triangle.SecondOrderBook.Remove(secondSymbolLowestAskKey, out var _);

                    triangle.FirstOrderBook[firstSymbolLowestAskKey] = triangle.FirstOrderBook[firstSymbolLowestAskKey] - bottleneck.Value / firstSymbolLowestAskKey; //first trade must be quoted in BTC terms
                    triangle.ThirdOrderBook[thirdSymbolHighestBidKey] = triangle.ThirdOrderBook[thirdSymbolHighestBidKey] - bottleneck.Value / thirdSymbolHighestBidKey; //last trade must be quoted in BTC terms

                }
                else
                {
                    triangle.ThirdOrderBook.Remove(thirdSymbolHighestBidKey, out var _);

                    triangle.FirstOrderBook[firstSymbolLowestAskKey] = triangle.FirstOrderBook[firstSymbolLowestAskKey] - bottleneck.Value / firstSymbolLowestAskKey; //first trade is quoted in btc terms
                    triangle.SecondOrderBook[secondSymbolLowestAskKey] = triangle.SecondOrderBook[secondSymbolLowestAskKey] - bottleneck.Value / triangle.FirstOrderBookVolumeConverter.Value / secondSymbolLowestAskKey; //second trade is quoted in alt terms, so convert using first orderbook.
                }
            }
            else if (triangle.Direction == Directions.BuySellSell)
            {
                var secondSymbolHighestBidKey = triangle.SecondOrderBook.Keys.Max(); //this only needs to be defined within this scope
                if (bottleneck.Key == Bottlenecks.FirstTrade)
                {
                    triangle.FirstOrderBook.Remove(firstSymbolLowestAskKey, out var _);

                    triangle.SecondOrderBook[secondSymbolHighestBidKey] = triangle.SecondOrderBook[secondSymbolHighestBidKey] - bottleneck.Value / thirdSymbolHighestBidKey / secondSymbolHighestBidKey; //second trade is quoted in alt terms, so convert using first orderbook.
                    triangle.ThirdOrderBook[thirdSymbolHighestBidKey] = triangle.ThirdOrderBook[thirdSymbolHighestBidKey] - bottleneck.Value / thirdSymbolHighestBidKey; //last trade must be quoted in BTC terms

                }
                else if (bottleneck.Key == Bottlenecks.SecondTrade)
                {
                    triangle.SecondOrderBook.Remove(secondSymbolHighestBidKey, out var _);

                    triangle.FirstOrderBook[firstSymbolLowestAskKey] = triangle.FirstOrderBook[firstSymbolLowestAskKey] - bottleneck.Value / firstSymbolLowestAskKey; //first trade must be quoted in BTC terms
                    triangle.ThirdOrderBook[thirdSymbolHighestBidKey] = triangle.ThirdOrderBook[thirdSymbolHighestBidKey] - bottleneck.Value / thirdSymbolHighestBidKey; //last trade must be quoted in BTC terms
                }
                else //bottleneck is third trade
                {
                    triangle.ThirdOrderBook.Remove(thirdSymbolHighestBidKey, out var _);

                    triangle.FirstOrderBook[firstSymbolLowestAskKey] = triangle.FirstOrderBook[firstSymbolLowestAskKey] - bottleneck.Value / firstSymbolLowestAskKey; //first trade must be quoted in BTC terms
                    triangle.SecondOrderBook[secondSymbolHighestBidKey] = triangle.SecondOrderBook[secondSymbolHighestBidKey] - bottleneck.Value / triangle.ThirdOrderBookVolumeConverter.Key / secondSymbolHighestBidKey; //second trade is quoted in alt terms, so convert using first orderbook.
                }
            }
            else // sell buy sell
            {
                if (bottleneck.Key == Bottlenecks.FirstTrade)
                {
                    triangle.FirstOrderBook.Remove(firstSymbolHighestBidKey, out var _);
                    //second trade depth is expressed in altcoin terms. to convert to BTC, use the third orderbook bid price
                    triangle.SecondOrderBook[secondSymbolLowestAskKey] = triangle.SecondOrderBook[secondSymbolLowestAskKey] - bottleneck.Value / thirdSymbolHighestBidKey; //second trade is quoted in alt terms, so convert using first orderbook.
                    triangle.ThirdOrderBook[thirdSymbolHighestBidKey] = triangle.ThirdOrderBook[thirdSymbolHighestBidKey] - bottleneck.Value / thirdSymbolHighestBidKey; //last trade must be quoted in BTC terms
                }
                else if (bottleneck.Key == Bottlenecks.SecondTrade)
                {
                    triangle.SecondOrderBook.Remove(secondSymbolLowestAskKey, out var _);

                    triangle.FirstOrderBook[firstSymbolHighestBidKey] = triangle.FirstOrderBook[firstSymbolHighestBidKey] - bottleneck.Value; //first trade depth is already in BTC terms
                    triangle.ThirdOrderBook[thirdSymbolHighestBidKey] = triangle.ThirdOrderBook[thirdSymbolHighestBidKey] - bottleneck.Value / thirdSymbolHighestBidKey; //last trade must be quoted in BTC terms
                }
                else //bottleneck is third trade
                {
                    triangle.ThirdOrderBook.Remove(thirdSymbolHighestBidKey, out var _);

                    triangle.FirstOrderBook[firstSymbolHighestBidKey] = triangle.FirstOrderBook[firstSymbolHighestBidKey] - bottleneck.Value; //first trade depth is already in BTC terms
                    triangle.SecondOrderBook[secondSymbolLowestAskKey] = triangle.SecondOrderBook[secondSymbolLowestAskKey] - bottleneck.Value / thirdSymbolHighestBidKey; //second trade is quoted in alt terms, so convert using third orderbook.
                }
            }
        }
    }
}