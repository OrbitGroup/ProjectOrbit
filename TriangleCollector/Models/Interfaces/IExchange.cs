using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.Json;



namespace TriangleCollector.Models.Interfaces
{
    public interface IExchange
    {
        public string ExchangeName { get; }

        public IExchangeClient ExchangeClient { get; }

        public List<IClientWebSocket> ActiveClients { get; }

        public List<IClientWebSocket> InactiveClients { get; }

        public Type OrderbookType { get; }

        public HashSet<IOrderbook> TradedMarkets { get; set; }

        public ConcurrentDictionary<string, IOrderbook> OfficialOrderbooks { get; }

        public ConcurrentQueue<Triangle> TrianglesToRecalculate { get; set; }

        public ConcurrentDictionary<string, Triangle> Triangles { get; }

        public HashSet<string> TriarbEligibleMarkets { get; set; }

        public bool QueuedSubscription { get; set; }

        public bool AggregateStreamOpen { get; set; }

        public ConcurrentQueue<IOrderbook> SubscriptionQueue { get; set; }

        public ConcurrentDictionary<string, IOrderbook> SubscribedMarkets { get; set; }

        public ConcurrentDictionary<string, List<Triangle>> TriangleTemplates { get; }

        public ConcurrentQueue<(bool, string)> OrderbookUpdateQueue {get;}

        public ConcurrentDictionary<string, int> OrderbookUpdateStats { get; set; }

        public int UniqueTriangleCount { get; set; }

        public ConcurrentDictionary<string, DateTime> ProfitableSymbolMapping { get; }

        public ConcurrentDictionary<string, DateTime> TriangleRefreshTimes { get; }

        public ConcurrentQueue<Triangle> RecalculatedTriangles { get; }

        public int TriangleCount { get; set; }

        public decimal TotalUSDValueProfitableTriangles { get; set; }
        public decimal TotalUSDValueViableTriangles { get; set; }
        public decimal EstimatedViableProfit { get; set; }
    }
}




