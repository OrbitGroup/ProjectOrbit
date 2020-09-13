using System;
using System.Collections.Generic;
using System.Text;
using TriangleCollector.Models;

namespace TriangleCollector.UnitTests.Models
{
    static class Orderbooks
    {
        class DescendingComparer<T> : IComparer<T> where T : IComparable<T>
        {
            public int Compare(T x, T y)
            {
                return y.CompareTo(x);
            }
        }
        //
        //new Triangle("ETHBTC", "EOSETH", "EOSBTC", Triangle.Directions.BuyBuySell, _factory.CreateLogger<Triangle>()}, 0.9924677859176048
        //new Triangle("EOSBTC", "EOSETH", "ETHBTC", Triangle.Directions.BuySellSell, _factory.CreateLogger<Triangle>()}, 0.9994362518556066
        //new Triangle("BTCUSD", "EOSUSD", "EOSBTC", Triangle.Directions.SellBuySell, _factory.CreateLogger<Triangle>()}, 0.9994800007008077
        public static Orderbook EthBtc = new Orderbook 
        { 
            SortedBids = new SortedDictionary<decimal, decimal>(new DescendingComparer<decimal>())
            {
                { 0.034139m, 4.2344m },
                { 0.034110m, 2.9281m },
                { 0.034070m, 6.0711m }
            },
            SortedAsks = new SortedDictionary<decimal, decimal>
            {
                {0.034172m, 3.6m},
                {0.034200m, 0.3235m},
                {0.035210m, 1.1731m }
            }
        };

        public static Orderbook EosEth = new Orderbook 
        {
            SortedBids = new SortedDictionary<decimal, decimal>(new DescendingComparer<decimal>())
            {
                {0.0080856m, 20m},
                {0.0080810m, 543.14m},
                {0.0080500m, 144.83m }
            },
            SortedAsks = new SortedDictionary<decimal, decimal>
            {
                {0.0081086m, 20m},
                {0.0081500m, 362.18m},
                {0.0081575m, 144.86m }
            }
        };

        public static Orderbook EosBtcUnprofitable = new Orderbook 
        {
            SortedAsks = new SortedDictionary<decimal, decimal>
            {
                {0.00027619m, 104.95m},
                {0.00027750m, 123.82m},
                {0.00027900m, 160.66m }
            },
            SortedBids = new SortedDictionary<decimal, decimal>(new DescendingComparer<decimal>())
            {
                {0.00027500m, 506.75m},
                {0.00027300m, 120.44m},
                {0.00027100m, 725.15m }
            }
        };
        //ORDERBOOKS FOR TESTING PROFITABLE BUY-BUY-SELL TRIANGLES (ONE FOR EACH BOTTLENECK)

        //BUYBUYSELL BOTTLENECK = TRADE 1 (USE PROFITABLE TEST ORDERBOOK FOR THIRD TRADE):
        public static Orderbook EthBtcBuyBuySellBottleneckOne = new Orderbook
        {
            SortedBids = new SortedDictionary<decimal, decimal>(new DescendingComparer<decimal>())
            {
                {0.034139m, 4.2344m},
                {0.034110m, 2.9281m},
                {0.034070m, 6.0711m }
            },
            SortedAsks = new SortedDictionary<decimal, decimal>
            {
                {0.034172m, 0.036m},
                {0.034200m, 0.3235m},
                {0.035210m, 1.1731m }
            }
        };

        public static Orderbook EosEthBuyBuySellBottleneckOne = new Orderbook
        {
            SortedBids = new SortedDictionary<decimal, decimal>(new DescendingComparer<decimal>())
            {
                {0.0080856m, 20m},
                {0.0080810m, 543.14m},
                {0.0080500m, 144.83m }
            },
            SortedAsks = new SortedDictionary<decimal, decimal>
            {
                {0.0081086m, 2000000m},
                {0.0081500m, 3620000.18m},
                {0.0081575m, 1440000.86m }
            }
        };


        //BUYBUYSELL BOTTLENECK = TRADE 2 (USE REGULAR TEST ORDER BOOKS FOR FIRST TWO TRADES): 
        public static Orderbook EosBtcProfitable = new Orderbook //since all of the unprofitable test values are very close to equilibrium, a 2% change in price here will make all triangles profitable
        {
            SortedAsks = new SortedDictionary<decimal, decimal> //asks are 2% lower (more favorable for buying)
            {
                {0.00027000m, 104.95m},
                {0.00027100m, 123.82m},
                {0.00027200m, 160.66m }
            },
            SortedBids = new SortedDictionary<decimal, decimal>(new DescendingComparer<decimal>()) //bids are 2% higher (more favorable for selling). It is practically impossible for bids to be higher than asks but that is fine for these purposes
            {
                {0.00028050m, 506.75m},
                {0.00028000m, 120.44m},
                {0.00027900m, 725.15m }
            }
        };

        //BUYBUYSELL BOTTLENECK = TRADE 3 (USE OTHER PROFITABLE TEST ORDERBOOK FOR SECOND AND THIRD TRADE):
        public static Orderbook EthBtcBuyBuySellBottleneckThree = new Orderbook
        {
            SortedBids = new SortedDictionary<decimal, decimal>(new DescendingComparer<decimal>())
            {
                {0.034139m, 4.2344m},
                {0.034110m, 2.9281m},
                {0.034070m, 6.0711m }
            },
            SortedAsks = new SortedDictionary<decimal, decimal>
            {
                {0.034172m, 36},
                {0.034200m, 32.35m},
                {0.035210m, 17.31m }
            }
        };


        //BUYSELLSELL BOTTLENECK = TRADE 3 (USE OTHER PROFITABLE TEST ORDERBOOK FOR SECOND AND THIRD TRADE):
        //use EOSBTCProfitable, and the normal orderbooks for everything else



        public static Orderbook BtcUsdSortedBids = new Orderbook 
        {
            SortedBids = new SortedDictionary<decimal, decimal>(new DescendingComparer<decimal>())
            {
                {10372.24m, 0.75m},
                {10370.04m, 0.12m},
                {10367.85m, 0.24m }
            } 
        };

        public static Orderbook EosUsdSortedAsks = new Orderbook 
        {
            SortedAsks = new SortedDictionary<decimal, decimal>
            {
                {2.85385m, 37.09m},
                {2.86429m, 600m},
                {2.86940m, 363.86m }
            } 
        };
    }
}
