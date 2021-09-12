using System;
using System.Collections.Generic;
using System.Linq;
using static TriangleCollector.Models.Triangle;

namespace TriangleCollector.Models
{
    public static class MaxVolumeCalculator
    {
        public static KeyValuePair<Bottlenecks, decimal> GetMaxVolume(Triangle triangle)
        {
            if (triangle.Direction == Directions.BuyBuySell)
            {
                //calculate the relevant lowest asks and highest bids once to avoid repeated calls to the min/max methods.
                decimal firstOrderbookLowestAskPrice = triangle.FirstOrderBook.Keys.Min();
                decimal secondOrderbookLowestAskPrice = triangle.SecondOrderBook.Keys.Min();
                decimal thirdOrderbookHighestBidPrice = triangle.ThirdOrderBook.Keys.Max();

                // since the first trade is quoted in BTC terms, the volume is simply the quantity available times the price.
                // the second trade is in the other base's terms, so you must convert the base-terms volume into BTC using the first trade price (which is base-BTC) 
                // Other than that, the logic is the same as the first trade since we are buying something again.

                decimal firstBtcVolume = Math.Max(firstOrderbookLowestAskPrice * triangle.FirstOrderBook[firstOrderbookLowestAskPrice],0);
                decimal secondBtcVolume = Math.Max(secondOrderbookLowestAskPrice * triangle.SecondOrderBook[secondOrderbookLowestAskPrice] * triangle.FirstOrderBookVolumeConverter.Key,0);
                // the third direction must be Sell at this point (there is no other potential combination)
                decimal thirdBtcVolume = Math.Max(thirdOrderbookHighestBidPrice * triangle.ThirdOrderBook[thirdOrderbookHighestBidPrice],0);
                //calculate and identify the bottleneck

                if (firstBtcVolume <= secondBtcVolume && firstBtcVolume <= thirdBtcVolume)
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.FirstTrade, firstBtcVolume);
                }
                else if (secondBtcVolume <= firstBtcVolume && secondBtcVolume <= thirdBtcVolume)
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.SecondTrade, secondBtcVolume);
                }
                else
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.ThirdTrade, thirdBtcVolume);
                }
            }
            else if (triangle.Direction == Directions.BuySellSell)
            {
                //calculate the lowest asks and highest bids once to avoid repeated calls to the Min/Max methods.
                decimal firstOrderbookLowestAskPrice = triangle.FirstOrderBook.Keys.Min();
                decimal secondOrderbookHighestBidPrice = triangle.SecondOrderBook.Keys.Max();
                decimal thirdOrderbookHighestBidPrice = triangle.ThirdOrderBook.Keys.Max();

                // since the first trade is quoted in BTC terms, the volume is simply the quantity available times the price.
                // second trade is a sell order, so the direction must be Buy Sell Sell
                // the depth is expressed in altcoin terms which must be converted to BTC. Price is expressed in basecoin terms.
                // the first order book contains the ALT-BTC price, which is therefore used to convert the volume to BTC terms
                decimal firstBtcVolume = Math.Max(firstOrderbookLowestAskPrice * triangle.FirstOrderBook[firstOrderbookLowestAskPrice],0);
                decimal secondBtcVolume = Math.Max(secondOrderbookHighestBidPrice * triangle.SecondOrderBook[secondOrderbookHighestBidPrice] * thirdOrderbookHighestBidPrice,0);
                // third trade is always in BTC price terms
                decimal thirdBtcVolume = Math.Max(thirdOrderbookHighestBidPrice * triangle.ThirdOrderBook[thirdOrderbookHighestBidPrice],0);
                //calculate and identify the bottleneck

                if (firstBtcVolume <= secondBtcVolume && firstBtcVolume <= thirdBtcVolume)
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.FirstTrade, firstBtcVolume);
                }
                else if (secondBtcVolume <= firstBtcVolume && secondBtcVolume <= thirdBtcVolume)
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.SecondTrade, secondBtcVolume);
                }
                else
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.ThirdTrade, thirdBtcVolume);
                }
            }
            else //Sell Buy Sell
            {
                //calculate the third orderbook's highest bid once to avoid repeated calls to Max method.
                decimal thirdOrderbookhighestBid = triangle.ThirdOrderBook.Keys.Max();

                //first trade is a sell order. only one direction starts with a sell order: Sell Buy Sell
                //the only scenario when the first trade is a sell order is USDT/TUSD based trades, in which depth is already expressed in BTC (price is expressed in USD)
                decimal firstBtcVolume = Math.Max(triangle.FirstOrderBook[triangle.FirstOrderBook.Keys.Max()],0);
                // for the second trade, the depth is expressed in the altcoin terms (price is expressed in USD). Therefore it just needs to be converted to BTC via the third order book.                
                decimal secondBtcVolume = Math.Max(triangle.SecondOrderBook[triangle.SecondOrderBook.Keys.Min()] * thirdOrderbookhighestBid,0);
                //the third trade is always in BTC price terms
                decimal thirdBtcVolume = Math.Max(thirdOrderbookhighestBid * triangle.ThirdOrderBook[thirdOrderbookhighestBid],0);
                //calculate and identify the bottleneck

                if (firstBtcVolume <= secondBtcVolume && firstBtcVolume <= thirdBtcVolume)
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.FirstTrade, firstBtcVolume);
                }
                else if (secondBtcVolume <= firstBtcVolume && secondBtcVolume <= thirdBtcVolume)
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.SecondTrade, secondBtcVolume);
                }
                else
                {
                    return new KeyValuePair<Bottlenecks, decimal>(Bottlenecks.ThirdTrade, thirdBtcVolume);
                }
            }
        }
    }
}