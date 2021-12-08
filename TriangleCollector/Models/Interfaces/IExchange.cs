using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Caching.Memory;

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

        public HashSet<string> TriarbEligibleMarkets { get; set; }

        public bool QueuedSubscription { get; set; }

        public bool AggregateStreamOpen { get; set; }

        public ConcurrentQueue<IOrderbook> SubscriptionQueue { get; set; }

        public ConcurrentDictionary<string, IOrderbook> SubscribedMarkets { get; set; }

        public ConcurrentDictionary<string, List<Triangle>> TriangleTemplates { get; }

        public int UniqueTriangleCount { get; set; }

        public ConcurrentDictionary<string, DateTime> ProfitableSymbolMapping { get; }

        public ConcurrentDictionary<string, DateTime> TriangleRefreshTimes { get; }

        public Channel<Triangle> TradeQueue { get; }

        public IMemoryCache RecentlyTradedTriangles { get; }
    }
}




