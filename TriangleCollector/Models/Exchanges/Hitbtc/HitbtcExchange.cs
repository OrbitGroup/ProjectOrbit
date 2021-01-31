using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models.Interfaces;
using TriangleCollector.Services;

namespace TriangleCollector.Models.Exchanges.Hitbtc
{
    public class HitbtcExchange : IExchange
    {
        public string ExchangeName { get; }

        public IExchangeClient ExchangeClient { get; } = new HitbtcClient();

        public List<IClientWebSocket> ActiveClients { get; } = new List<IClientWebSocket>();
        public List<IClientWebSocket> InactiveClients { get; } = new List<IClientWebSocket>();

        public Type OrderbookType { get; } = typeof(HitbtcOrderbook);

        public HashSet<IOrderbook> TradedMarkets { get; set; } = new HashSet<IOrderbook>();

        public ConcurrentDictionary<string, IOrderbook> OfficialOrderbooks { get; } = new ConcurrentDictionary<string, IOrderbook>();

        public ConcurrentQueue<Triangle> TrianglesToRecalculate { get; set; } = new ConcurrentQueue<Triangle>();

        public ConcurrentDictionary<string, Triangle> Triangles { get; } = new ConcurrentDictionary<string, Triangle>();

        public HashSet<string> TriarbEligibleMarkets { get; set; } = new HashSet<string>();

        public ConcurrentQueue<IOrderbook> SubscriptionQueue { get; set; } = new ConcurrentQueue<IOrderbook>();

        public bool QueuedSubscription { get; set; } = true;

        public bool AggregateStreamOpen { get; set; } = false;

        public ConcurrentDictionary<string, IOrderbook> SubscribedMarkets { get; set; } = new ConcurrentDictionary<string, IOrderbook>();

        public ConcurrentDictionary<string, List<Triangle>> TriarbMarketMapping { get; } = new ConcurrentDictionary<string, List<Triangle>>();

        public int UniqueTriangleCount { get; set; } = 0;

        public ConcurrentQueue<(bool, string)> OrderbookUpdateQueue { get; } = new ConcurrentQueue<(bool, string)>();

        public ConcurrentDictionary<string, int> OrderbookUpdateStats { get; set; } = new ConcurrentDictionary<string, int>();

        public ConcurrentDictionary<string, DateTime> ProfitableSymbolMapping { get; } = new ConcurrentDictionary<string, DateTime>();

        public ConcurrentDictionary<string, DateTime> TriangleRefreshTimes { get; } = new ConcurrentDictionary<string, DateTime>();

        public ConcurrentQueue<Triangle> RecalculatedTriangles { get; } = new ConcurrentQueue<Triangle>();

        public int TriangleCount { get; set; }

        public decimal TotalUSDValueProfitableTriangles { get; set; }
        public decimal TotalUSDValueViableTriangles { get; set; }
        public decimal EstimatedViableProfit { get; set; }

        public HitbtcExchange(string name)
        {

            ExchangeName = name;
            ExchangeClient.Exchange = this;

        }
    }
}

