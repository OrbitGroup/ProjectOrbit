using System.Collections.Generic;
using System.Linq;
using TriangleCollector.Models.Interfaces;
using static TriangleCollector.Models.Triangle;

namespace TriangleCollector.Models
{
    public static class CreateSnapshots
    {
        public static void CreateOrderbookSnapshots(Triangle triangle)
        {
            lock(triangle.ThirdSymbolOrderbook.OrderbookLock)
            {
                triangle.ThirdOrderBook = new Dictionary<decimal, decimal>(triangle.ThirdSymbolOrderbook.OfficialBids);
            }
            
            if (triangle.Direction == Directions.BuyBuySell)
            {
                lock(triangle.FirstSymbolOrderbook.OrderbookLock)
                {
                    triangle.FirstOrderBook = new Dictionary<decimal, decimal>(triangle.FirstSymbolOrderbook.OfficialAsks);
                }

                lock(triangle.SecondSymbolOrderbook.OrderbookLock)
                {
                    triangle.SecondOrderBook = new Dictionary<decimal, decimal>(triangle.SecondSymbolOrderbook.OfficialAsks);
                }
                
                decimal highestBid = triangle.FirstSymbolOrderbook.OfficialBids.Keys.Max();
                triangle.FirstOrderBookVolumeConverter = new KeyValuePair<decimal, decimal>(highestBid, triangle.FirstSymbolOrderbook.OfficialBids[highestBid]);
            }
            else if (triangle.Direction == Directions.BuySellSell)
            {
                lock (triangle.FirstSymbolOrderbook.OrderbookLock)
                {
                    triangle.FirstOrderBook = new Dictionary<decimal, decimal>(triangle.FirstSymbolOrderbook.OfficialAsks);
                }
                
                lock (triangle.SecondSymbolOrderbook.OrderbookLock)
                {
                    triangle.SecondOrderBook = new Dictionary<decimal, decimal>(triangle.SecondSymbolOrderbook.OfficialBids);
                }
                
                decimal lowestAsk = triangle.ThirdSymbolOrderbook.OfficialAsks.Keys.Min();
                triangle.ThirdOrderBookVolumeConverter = new KeyValuePair<decimal, decimal>(lowestAsk, triangle.ThirdSymbolOrderbook.OfficialAsks[lowestAsk]);
            }
            else //SellBuySell
            {
                lock (triangle.FirstSymbolOrderbook.OrderbookLock)
                {
                    triangle.FirstOrderBook = new Dictionary<decimal, decimal>(triangle.FirstSymbolOrderbook.OfficialBids);
                }
                lock (triangle.SecondSymbolOrderbook.OrderbookLock)
                {
                    triangle.SecondOrderBook = new Dictionary<decimal, decimal>(triangle.SecondSymbolOrderbook.OfficialAsks);
                }
            }
        }
    }
}