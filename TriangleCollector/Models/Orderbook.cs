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
        
        public ConcurrentDictionary<decimal, decimal> bids { get; set; }

        public DateTime timestamp { get; set; }

        /// <summary>
        /// Takes an update and merges it with this orderbook.
        /// </summary>
        /// <param name="update">An orderbook update</param>
        public void Merge(Orderbook update)
        {
            if (this.sequence < update.sequence)
            {
                this.sequence = update.sequence;
                this.timestamp = update.timestamp;
                this.method = update.method;
                
                //Loop through update.asks and update.bids in parallel and either add them to this.asks and this.bids or update the value thats currently there.
                update.asks.AsParallel().ForAll(x => asks.AddOrUpdate(x.Key, x.Value, (key, oldValue) => oldValue = x.Value));
                update.bids.AsParallel().ForAll(x => bids.AddOrUpdate(x.Key, x.Value, (key, oldValue) => oldValue = x.Value));
            }
        }
    }
}



