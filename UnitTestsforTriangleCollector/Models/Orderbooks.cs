using System;
using System.Collections.Generic;
using System.Text;
using TriangleCollector.Models;

namespace TriangleCollector.UnitTests.Models
{
    static class Orderbooks
    {
        //new Triangle("ETHBTC", "EOSETH", "EOSBTC", Triangle.Directions.BuyBuySell, _factory.CreateLogger<Triangle>()), 0.9924677859176048
        //new Triangle("EOSBTC", "EOSETH", "ETHBTC", Triangle.Directions.BuySellSell, _factory.CreateLogger<Triangle>()), 0.9994362518556066
        //new Triangle("BTCUSD", "EOSUSD", "EOSBTC", Triangle.Directions.SellBuySell, _factory.CreateLogger<Triangle>()), 0.9994800007008077
        public static Orderbook EthBtc = new Orderbook 
        { 
            SortedBids = new KeyValuePair<decimal, decimal>[] 
            { 
                new KeyValuePair<decimal, decimal>((decimal)0.034139, (decimal)4.2344),
                new KeyValuePair<decimal, decimal>((decimal)0.034110, (decimal)2.9281),
                new KeyValuePair<decimal, decimal>((decimal)0.034070, (decimal)6.0711)
            },
            SortedAsks = new KeyValuePair<decimal, decimal>[]
            {
                new KeyValuePair<decimal, decimal>((decimal)0.034172, (decimal)3.6),
                new KeyValuePair<decimal, decimal>((decimal)0.034200, (decimal)0.3235),
                new KeyValuePair<decimal, decimal>((decimal)0.035210, (decimal)1.1731)
            }
        };

        public static Orderbook EosEth = new Orderbook 
        { 
            SortedBids = new KeyValuePair<decimal, decimal>[] 
            { 
                new KeyValuePair<decimal, decimal>((decimal)0.0080856, (decimal)20),
                new KeyValuePair<decimal, decimal>((decimal)0.0080810, (decimal)543.14),
                new KeyValuePair<decimal, decimal>((decimal)0.0080500, (decimal)144.83)
            },
            SortedAsks = new KeyValuePair<decimal, decimal>[]
            {
                new KeyValuePair<decimal, decimal>((decimal)0.0081086, (decimal)20),
                new KeyValuePair<decimal, decimal>((decimal)0.0081500, (decimal)362.18),
                new KeyValuePair<decimal, decimal>((decimal)0.0081575, (decimal)144.86)
            }
        };

        public static Orderbook EosBtcUnprofitable = new Orderbook 
        { 
            SortedAsks = new KeyValuePair<decimal, decimal>[] 
            { 
                new KeyValuePair<decimal, decimal>((decimal)0.00027619, (decimal)104.95),
                new KeyValuePair<decimal, decimal>((decimal)0.00027750, (decimal)123.82),
                new KeyValuePair<decimal, decimal>((decimal)0.00027900, (decimal)160.66)
            },
            SortedBids = new KeyValuePair<decimal, decimal>[]
            {
                new KeyValuePair<decimal, decimal>((decimal)0.00027500, (decimal)506.75),
                new KeyValuePair<decimal, decimal>((decimal)0.00027300, (decimal)120.44),
                new KeyValuePair<decimal, decimal>((decimal)0.00027100, (decimal)725.15)
            }
        };

        public static Orderbook EosBtcProfitable = new Orderbook //since all of the unprofitable test values are very close to equilibrium, a 2% change in price here will make all triangles profitable
        {
            SortedAsks = new KeyValuePair<decimal, decimal>[] //asks are 2% lower (more favorable for buying)
            {
                new KeyValuePair<decimal, decimal>((decimal)0.00027000, (decimal)104.95),
                new KeyValuePair<decimal, decimal>((decimal)0.00027100, (decimal)123.82),
                new KeyValuePair<decimal, decimal>((decimal)0.00027200, (decimal)160.66)
            },
            SortedBids = new KeyValuePair<decimal, decimal>[] //bids are 2% higher (more favorable for selling). It is practically impossible for bids to be higher than asks but that is fine for these purposes
            {
                new KeyValuePair<decimal, decimal>((decimal)0.00028050, (decimal)506.75),
                new KeyValuePair<decimal, decimal>((decimal)0.00028000, (decimal)120.44),
                new KeyValuePair<decimal, decimal>((decimal)0.00027900, (decimal)725.15)
            }
        };

        public static Orderbook BtcUsdSortedBids = new Orderbook 
        { 
            SortedBids = new KeyValuePair<decimal, decimal>[] 
            { 
                new KeyValuePair<decimal, decimal>((decimal)10372.24, (decimal)0.75),
                new KeyValuePair<decimal, decimal>((decimal)10370.04, (decimal)0.12),
                new KeyValuePair<decimal, decimal>((decimal)10367.85, (decimal)0.24)
            } 
        };

        public static Orderbook EosUsdSortedAsks = new Orderbook 
        { 
            SortedAsks = new KeyValuePair<decimal, decimal>[] 
            { 
                new KeyValuePair<decimal, decimal>((decimal)2.85385, (decimal)37.09),
                new KeyValuePair<decimal, decimal>((decimal)2.86429, (decimal)600),
                new KeyValuePair<decimal, decimal>((decimal)2.86940, (decimal)363.86)
            } 
        };
    }
}
