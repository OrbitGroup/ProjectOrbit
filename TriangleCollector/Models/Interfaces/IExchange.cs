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

        public List<IClientWebSocket> Clients { get; }

        public Type OrderbookType { get; }

        public HashSet<IOrderbook> TradedMarkets { get; set; }

        public ConcurrentDictionary<string, IOrderbook> OfficialOrderbooks { get; }

        public ConcurrentQueue<Triangle> TrianglesToRecalculate { get; set; }

        public ConcurrentDictionary<string, Triangle> Triangles { get; }

        public HashSet<string> TriarbEligibleMarkets { get; set; }

        public Queue<IOrderbook> SubscriptionQueue { get; set; } 
        public ConcurrentDictionary<string, IOrderbook> SubscribedMarkets { get; set; }

        public ConcurrentDictionary<string, List<Triangle>> TriarbMarketMapping { get; }

        public double ImpactedTriangleCounter { get; set; }

        public double RedundantTriangleCounter { get; set; }

        public double AllOrderBookCounter { get; set; }

        public double InsideLayerCounter { get; set; }

        public double OutsideLayerCounter { get; set; }

        public double PositivePriceChangeCounter { get; set; }

        public double NegativePriceChangeCounter { get; set; }

        public int UniqueTriangleCount { get; set; }

        public ConcurrentDictionary<string, int> ProfitableSymbolMapping { get; }

        public ConcurrentDictionary<string, DateTime> TriangleRefreshTimes { get; }

        public ConcurrentQueue<Triangle> RecalculatedTriangles { get; }
    }
}




