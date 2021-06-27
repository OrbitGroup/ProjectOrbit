using System.Linq;
using static TriangleCollector.Models.Triangle;

namespace TriangleCollector.Models
{
    public static class ProfitPercentCalculator
    {
        public static decimal GetProfitPercent(Triangle triangle)
        {
            //use the direction list to understand what trades to make at each step
            if (triangle.Direction == Directions.BuySellSell)
            {
                var firstTrade = 1 / triangle.FirstOrderBook.Keys.Min();
                var secondTrade = firstTrade * triangle.SecondOrderBook.Keys.Max(); //sell
                var thirdTrade = secondTrade * triangle.ThirdOrderBook.Keys.Max(); //sell
                return thirdTrade - 1;
            }
            else if (triangle.Direction == Directions.BuyBuySell)
            {
                var firstTrade = 1 / triangle.FirstOrderBook.Keys.Min();
                var secondTrade = firstTrade / triangle.SecondOrderBook.Keys.Min(); //buy
                var thirdTrade = secondTrade * triangle.ThirdOrderBook.Keys.Max(); //sell
                return thirdTrade - 1;
            }
            else //Sell Buy Sell
            {
                var firstTrade = 1 * triangle.FirstOrderBook.Keys.Max();
                var secondTrade = firstTrade / triangle.SecondOrderBook.Keys.Min();
                var thirdTrade = secondTrade * triangle.ThirdOrderBook.Keys.Max();
                return thirdTrade - 1;
            }

        }
    }
}