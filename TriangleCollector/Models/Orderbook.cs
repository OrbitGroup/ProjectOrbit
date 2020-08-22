using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

        public IOrderedEnumerable<KeyValuePair<decimal, decimal>> SortedAsks { get; set; }

        public ConcurrentDictionary<decimal, decimal> bids { get; set; }

        public IOrderedEnumerable<KeyValuePair<decimal, decimal>> SortedBids { get; set; }

        public DateTime timestamp { get; set; }

        public decimal LowestAsk { get; set; }

        public decimal HighestBid { get; set; }

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


                //SortedBids = new SortedDictionary<decimal, decimal>(bids).Reverse().ToDictionary(x => x.Key, y => y.Value);

                SortedBids = bids.OrderByDescending(layer => layer.Key);
                SortedAsks = asks.OrderBy(layer => layer.Key);

                var oldHighestBid = HighestBid;
                var oldLowestAsk = LowestAsk;

                HighestBid = SortedBids.First().Key;
                LowestAsk = SortedAsks.First().Key;

                if (oldHighestBid == HighestBid && oldLowestAsk == LowestAsk)
                {
                    return false;
                }

                return true;
            }

            return false;
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



