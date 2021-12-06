using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace TriangleCollector.Models.Interfaces
{
    public interface IOrderbook
    {

        public string Symbol { get; set; }

        public string BaseCurrency { get; set; } //the base currency expresses the currency in which the quantity of the order is expressed. I.E. when you are buying/selling ETHBTC you are buying selling a qty of ETH.
        
        public string QuoteCurrency { get; set; } //quote currency expresses the currency in which the price of this market is quoted. I.E. 'ETHBTC's price is quoted in BTC.
        
        public IExchange Exchange { get; set; } //which exchange the market belongs to

        public long Sequence { get; set; }

        public ConcurrentDictionary<decimal, decimal> OfficialAsks { get; set; }
        
        public SortedDictionary<decimal, decimal> SortedAsks { get; set; }

        public ConcurrentDictionary<decimal, decimal> OfficialBids { get; set; }
        
        public SortedDictionary<decimal, decimal> SortedBids { get; set; }

        public DateTime Timestamp { get; set; }

        public decimal LowestAsk { get; set; }

        public bool Pong { get; set; } //flagged when an exchange server requires that we send a 'pong' message to remain connected

        public long PongValue { get; set; } //contains the required pong value, if required

        public decimal HighestBid { get; set; }
        public decimal PreviousLowestAsk { get; set; }
        public decimal PreviousHighestBid { get; set; }

        public object OrderbookLock { get; }

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
        public (bool IsSignificant, string Category) Merge(IOrderbook update);

        public (bool IsSignificant, string Category) SignificantChange(IOrderbook updatedOrderbook); //TO DO: add flagging system to simply flag triangles as profitable and therefore signficant

        public void UpdateAskLayer(KeyValuePair<decimal, decimal> layer);

        public void UpdateBidLayer(KeyValuePair<decimal, decimal> layer);

        public IOrderbook DeepCopy();
    }
}
