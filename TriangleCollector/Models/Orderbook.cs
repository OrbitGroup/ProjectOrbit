using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

        public ConcurrentDictionary<decimal, decimal> officialAsks { get; set; }
        public SortedDictionary<decimal, decimal> SortedAsks { get; set; } = new SortedDictionary<decimal, decimal>();

        public ConcurrentDictionary<decimal, decimal> officialBids { get; set; }
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

        public Orderbook DeepCopy()
        {
            Orderbook DeepCopy = (Orderbook)this.MemberwiseClone();
            DeepCopy.officialAsks = new ConcurrentDictionary<decimal, decimal>(officialAsks);
            DeepCopy.officialBids = new ConcurrentDictionary<decimal, decimal>(officialBids);            
            return (DeepCopy);
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

                var previousLowestAsk = officialAsks.Keys.Min();
                var previousHighestBid = officialBids.Keys.Max();

                //Loop through update.asks and update.bids in parallel and either add them to this.asks and this.bids or update the value thats currently there.
                update.officialAsks.AsParallel().ForAll(UpdateAskLayer);
                update.officialBids.AsParallel().ForAll(UpdateBidLayer);

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
            if (TriangleCollector.ProfitableSymbolMapping.TryGetValue(symbol, out var layers)) //This symbol has had a profitable triangle this session with a max of N layers affected
            {
                CreateSorted();
                if ((update.officialAsks.Count > 0 && update.officialAsks.Keys.Min() < SortedAsks.Keys.ElementAt(layers)) || (update.officialBids.Count > 0 && update.officialBids.Keys.Max() > SortedBids.Keys.ElementAt(layers)))
                {
                    TriangleCollector.InsideLayerCounter++;
                    return true;
                } else
                {
                    TriangleCollector.OutsideLayerCounter++;
                    return false;
                }
            } 
            else //symbol is not mapped as profitable - update is only significant if the bottom bid/ask layers changed, and the price improved
            {
                if (officialAsks.Keys.Min() < previousLowestAsk || officialBids.Keys.Max() > previousHighestBid) //if the lowest ask price got lower, or the highest bid got higher, this is a universally better price that will always improve profitability
                {
                    TriangleCollector.PositivePriceChangeCounter++;
                    return true;
                } else //price got worse or did not change
                {
                    TriangleCollector.NegativePriceChangeCounter++;
                    return false;
                }
            }
        }

        public void CreateSorted()
        {
            //var previousHighestBid = HighestBid;
            if (officialBids.Count > 0 && (SortedBids.Count == 0 || !SortedBids.TryGetValue(HighestBid, out _)))
            {
                SortedBids = new SortedDictionary<decimal, decimal>(officialBids, new DescendingComparer<decimal>());
                HighestBid = SortedBids.First().Key;
            }

            //var previousLowestAsk = LowestAsk;
            if (officialAsks.Count > 0 && (SortedAsks.Count == 0 || !SortedAsks.TryGetValue(LowestAsk, out _)))
            {
                SortedAsks = new SortedDictionary<decimal, decimal>(officialAsks);
                LowestAsk = SortedAsks.First().Key;
            }
        }

        private void UpdateAskLayer(KeyValuePair<decimal, decimal> layer)
        {
            if (layer.Value > 0)
            {
                officialAsks.AddOrUpdate(layer.Key, layer.Value, (key, oldValue) => oldValue = layer.Value);
            }
            else
            {
                officialAsks.TryRemove(layer.Key, out var _);
            }
        }

        private void UpdateBidLayer(KeyValuePair<decimal, decimal> layer)
        {
            if (layer.Value > 0)
            {
                officialBids.AddOrUpdate(layer.Key, layer.Value, (key, oldValue) => oldValue = layer.Value);
            }
            else
            {
                officialBids.TryRemove(layer.Key, out var _);
            }
        }
    }
}



