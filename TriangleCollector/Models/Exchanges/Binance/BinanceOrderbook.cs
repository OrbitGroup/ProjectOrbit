using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using TriangleCollector.Models.Interfaces;
using static TriangleCollector.Models.Interfaces.IOrderbook;

namespace TriangleCollector.Models.Exchanges.Binance
{
    [JsonConverter(typeof(BinanceConverter))]
    public class BinanceOrderbook : IOrderbook
    {
        public string Symbol { get; set; } = string.Empty;
        public string BaseCurrency { get; set; }
        public string QuoteCurrency { get; set; }
        public IExchange Exchange { get; set; }
        public long Sequence { get; set; }
        public ConcurrentDictionary<decimal, decimal> OfficialAsks { get; set; } = new ConcurrentDictionary<decimal, decimal>();
        public SortedDictionary<decimal, decimal> SortedAsks { get; set; } = new SortedDictionary<decimal, decimal>();
        public ConcurrentDictionary<decimal, decimal> OfficialBids { get; set; } = new ConcurrentDictionary<decimal, decimal>();
        public SortedDictionary<decimal, decimal> SortedBids { get; set; } = new SortedDictionary<decimal, decimal>();
        public DateTime Timestamp { get; set; }
        public decimal LowestAsk { get; set; }
        public bool Pong { get; set; }
        public long PongValue { get; set; }
        public decimal HighestBid { get; set; }
        public decimal PreviousLowestAsk { get; set; }
        public decimal PreviousHighestBid { get; set; }

        public object OrderbookLock { get; } = new object();

        public void CreateSorted()
        {
            //var previousHighestBid = HighestBid;
            if (OfficialBids.Count > 0 && (SortedBids.Count == 0 || !SortedBids.TryGetValue(HighestBid, out _)))
            {
                SortedBids = new SortedDictionary<decimal, decimal>(OfficialBids, new DescendingComparer<decimal>());
                HighestBid = SortedBids.First().Key;
            }

            //var previousLowestAsk = LowestAsk;
            if (OfficialAsks.Count > 0 && (SortedAsks.Count == 0 || !SortedAsks.TryGetValue(LowestAsk, out _)))
            {
                SortedAsks = new SortedDictionary<decimal, decimal>(OfficialAsks);
                LowestAsk = SortedAsks.First().Key;
            }
        }

        public bool Merge(IOrderbook update)
        {
            if (this.Sequence < update.Sequence)
            {
                this.Sequence = update.Sequence;
                this.Timestamp = update.Timestamp;

                if (OfficialAsks.Count() != 0 && OfficialBids.Count() != 0)
                {
                    PreviousLowestAsk = OfficialAsks.Keys.Min();
                    PreviousHighestBid = OfficialBids.Keys.Max();
                }
                else
                {
                    PreviousLowestAsk = 0;
                    PreviousHighestBid = decimal.MaxValue;
                }

                OfficialAsks = update.OfficialAsks;
                OfficialBids = update.OfficialBids;

                var significantChange = SignificantChange(update);
                Exchange.OrderbookUpdateQueue.Enqueue(significantChange);
                return significantChange.Item1;
            }

            return false;
        }
        public (bool, string) SignificantChange(IOrderbook updatedOrderbook)
        {
            if (Exchange.ProfitableSymbolMapping.TryGetValue(Symbol, out var lastProfitable))
            {
                if (DateTime.UtcNow - lastProfitable < TimeSpan.FromMinutes(1)) //if the symbol had a profitable triangle in the last minute
                {
                    return (true, "Symbol had a profitable triangle within the last minute");
                }
                else
                {
                    if (OfficialAsks.Keys.Min() < PreviousLowestAsk || OfficialBids.Keys.Max() > PreviousHighestBid)
                    {
                        return (true, "Price improved");
                    }
                    else //price got worse or did not change
                    {
                        return (false, "Price worsened");
                    }
                }

            }
            else //symbol is not mapped as profitable - update is only significant if the bottom bid/ask layers changed, and the price improved
            {
                if (OfficialAsks.Count < 1 || OfficialBids.Count < 1)
                {
                    return (false, "No OfficialAsks/Bids");
                }

                if (OfficialAsks.Keys.Min() < PreviousLowestAsk || OfficialBids.Keys.Max() > PreviousHighestBid) //if the lowest ask price got lower, or the highest bid got higher, this is a universally better price that will always improve profitability
                {
                    return (true, "Price improved");
                }
                else //price got worse or did not change
                {
                    return (false, "Price worsened");
                }
            }
        }

        public void UpdateAskLayer(KeyValuePair<decimal, decimal> layer)
        {
            if (layer.Value > 0)
            {
                OfficialAsks.AddOrUpdate(layer.Key, layer.Value, (key, oldValue) => oldValue = layer.Value);
            }
            else
            {
                OfficialAsks.TryRemove(layer.Key, out var _);
            }
        }

        public void UpdateBidLayer(KeyValuePair<decimal, decimal> layer)
        {
            if (layer.Value > 0)
            {
                OfficialBids.AddOrUpdate(layer.Key, layer.Value, (key, oldValue) => oldValue = layer.Value);
            }
            else
            {
                OfficialBids.TryRemove(layer.Key, out var _);
            }
        }

        public IOrderbook DeepCopy()
        {
            IOrderbook deepCopy = (IOrderbook)this.MemberwiseClone();
            deepCopy.OfficialAsks = new ConcurrentDictionary<decimal, decimal>(OfficialAsks);
            deepCopy.OfficialBids = new ConcurrentDictionary<decimal, decimal>(OfficialBids);
            return (deepCopy);
        }
    }
}



