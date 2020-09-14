﻿using System;
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

                //Loop through update.asks and update.bids in parallel and either add them to this.asks and this.bids or update the value thats currently there.
                update.asks.AsParallel().ForAll(UpdateAskLayer);
                update.bids.AsParallel().ForAll(UpdateBidLayer);

                if (SignificantChange())
                {
                    return true;
                }

                return false;
            }
            
            return false;
        }

        public bool SignificantChange()
        {
            // TODO: Create a more scientific approach for determining if we should recalculate a triangle
            if (bids.Count > 0 && asks.Count > 0 && !asks.TryGetValue(LowestAsk, out _) && !bids.TryGetValue(HighestBid, out _)) 
            {
                return true;
            }

            return false;
        }

        public void CreateSorted()
        {
            lock(orderbookLock)
            {
                if (bids.Count > 0 && !bids.Keys.OrderBy(x => x).SequenceEqual(SortedBids.Keys.OrderBy(x => x)))
                {
                    SortedBids = new SortedDictionary<decimal, decimal>(bids, new DescendingComparer<decimal>());
                    HighestBid = SortedBids.First().Key;
                }

                if (asks.Count > 0 && !asks.Keys.OrderBy(x => x).SequenceEqual(SortedAsks.Keys.OrderBy(x => x)))
                {
                    SortedAsks = new SortedDictionary<decimal, decimal>(asks);
                    LowestAsk = SortedAsks.First().Key;
                }
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



