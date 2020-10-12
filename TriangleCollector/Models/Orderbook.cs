using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TriangleCollector.Models
{
    [JsonConverter(typeof(OrderbookConverter))]
    public class Orderbook
    {
        public string symbol { get; set; }

        public int sequence { get; set; }

        public string method { get; set; }

        public ConcurrentDictionary<decimal, decimal> asks { get; set; }

        public SortedDictionary<decimal, decimal> SortedAsks { get; set; } = new SortedDictionary<decimal, decimal>();

        public ConcurrentDictionary<decimal, decimal> bids { get; set; }

        public SortedDictionary<decimal, decimal> SortedBids { get; set; } = new SortedDictionary<decimal, decimal>();

        public DateTime timestamp { get; set; }

        public decimal LowestAsk { get; set; }

        public decimal HighestBid { get; set; }

        public readonly object orderbookLock = new object();

        class DescendingComparer<T> : IComparer<T> where T : IComparable<T>
        {
            public int Compare(T x, T y)
            {
                return y.CompareTo(x);
            }
        }

        /// <summary>
        /// Takes an update and merges it with this orderbook.
        /// </summary>
        /// <param name="update">An orderbook update</param>
        public bool Merge(Orderbook update)
        {
            if (this.sequence < update.sequence)
            {
                this.sequence = update.sequence;
                this.timestamp = update.timestamp;
                this.method = update.method;

                TriangleCollector.allOrderBookCounter++;

                var previousLowestAsk = LowestAsk;
                var previousHighestBid = HighestBid;

                //Loop through update.asks and update.bids in parallel and either add them to this.asks and this.bids or update the value thats currently there.
                update.asks.AsParallel().ForAll(UpdateAskLayer);
                update.bids.AsParallel().ForAll(UpdateBidLayer);

                if (SignificantChange(update, previousHighestBid, previousLowestAsk))
                {
                    return true;
                }

                return false;
            }
            
            return false;
        }

        public bool SignificantChange(Orderbook update, decimal previousHighestBid, decimal previousLowestAsk)
        {
            // TODO: Create a more scientific approach for determining if we should recalculate a triangle
            //Approach: if the triangle's last run wasn't profitable, then a significant change is only a change to the lowest layer of the orderbook.
            // if the triangle's last run was profitable, then a significant change is a change to the layers that the triangle works with (TBD how to do that)
            if (TriangleCollector.ProfitableSymbolMapping.TryGetValue(symbol, out var layers))
            {
                if ((update.asks.Count > 0 && update.asks.Keys.Min() < SortedAsks.Keys.ElementAt(layers)) || (update.bids.Count > 0 && update.bids.Keys.Max() > SortedBids.Keys.ElementAt(layers)))
                {
                    TriangleCollector.LayerCounter++;
                    return true;
                }
                //
                //the variable layers is the number of layers that get accessed for this symbol.
                //the goal is to use that number of layers in conjunction with the SortedBids/Asks to determine whether the price update is within that number
                //of layers in the orderbook.

            } 
            else //symbol is not mapped as profitable - update is only significant if the bottom bid/ask layers changed
            {
                if (previousLowestAsk != asks.Keys.Min() || previousHighestBid != bids.Keys.Max()) 
                {
                    TriangleCollector.BestPriceChangeCounter++;
                    return true;
                }
            }

            return false;
        }

        public void CreateSorted()
        {
            var previousHighestBid = HighestBid;
            if (bids.Count > 0 && (SortedBids.Count == 0 || !SortedBids.TryGetValue(HighestBid, out _)))
            {
                SortedBids = new SortedDictionary<decimal, decimal>(bids, new DescendingComparer<decimal>());
                HighestBid = SortedBids.First().Key;
            }

            var previousLowestAsk = LowestAsk;
            if (asks.Count > 0 && (SortedAsks.Count == 0 || !SortedAsks.TryGetValue(LowestAsk, out _)))
            {
                SortedAsks = new SortedDictionary<decimal, decimal>(asks);
                LowestAsk = SortedAsks.First().Key;
            }

            if (HighestBid != previousHighestBid || LowestAsk != previousLowestAsk)
            {
                TriangleCollector.CreateSortedCounter++;
            }
        }

        private void UpdateAskLayer(KeyValuePair<decimal, decimal> layer)
        {
            if (layer.Value > 0)
            {
                asks.AddOrUpdate(layer.Key, layer.Value, (key, oldValue) => oldValue = layer.Value);
            }
            else
            {
                asks.TryRemove(layer.Key, out var _);
            }
        }

        private void UpdateBidLayer(KeyValuePair<decimal, decimal> layer)
        {
            if (layer.Value > 0)
            {
                bids.AddOrUpdate(layer.Key, layer.Value, (key, oldValue) => oldValue = layer.Value);
            }
            else
            {
                bids.TryRemove(layer.Key, out var _);
            }
        }
    }
}



