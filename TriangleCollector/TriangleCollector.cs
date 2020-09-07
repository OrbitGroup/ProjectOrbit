using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TriangleCollector.Models;
using TriangleCollector.Services;

namespace TriangleCollector
{
    public class TriangleCollector
    {

        private const string Uri = "wss://api.hitbtc.com/api/2/ws";

        public static ConcurrentDictionary<string, Orderbook> OfficialOrderbooks = new ConcurrentDictionary<string, Orderbook>();

        public static ConcurrentDictionary<string, Triangle> Triangles = new ConcurrentDictionary<string, Triangle>();

        public static ConcurrentDictionary<string, DateTime> TriangleRefreshTimes = new ConcurrentDictionary<string, DateTime>();

        public static ConcurrentDictionary<string, List<Triangle>> SymbolTriangleMapping = new ConcurrentDictionary<string, List<Triangle>>();

        public static HashSet<string> triangleEligiblePairs = new HashSet<string>();

        public static ConcurrentBag<string> ActiveSymbols = new ConcurrentBag<string>();

        public static ConcurrentQueue<string> UpdatedSymbols = new ConcurrentQueue<string>();

        public static ConcurrentQueue<Triangle> TrianglesToRecalculate = new ConcurrentQueue<Triangle>();

        public static ConcurrentQueue<Triangle> RecalculatedTriangles = new ConcurrentQueue<Triangle>();

        //stats data structures:
        public static ConcurrentQueue<long> MergeTimings = new ConcurrentQueue<long>();

        public static ConcurrentQueue<TimeSpan> OrderbookUpdateDeltas = new ConcurrentQueue<TimeSpan>();

        public static List<WebSocketAdapter> Clients = new List<WebSocketAdapter>();

        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.AddConsole();
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<QueueMonitor>();
                services.AddHostedService<SymbolMonitor>();
                services.AddHostedService<OrderbookSubscriber>();
                services.AddHostedService<TriangleCalculator>();
                services.AddHostedService<StatisticsMonitor>();
                //services.AddHostedService<TrianglePublisher>();
            });

        public static async Task<WebSocketAdapter> GetExchangeClientAsync()
        {
            var client = new ClientWebSocket();
            var factory = new LoggerFactory();
            var adapter = new WebSocketAdapter(factory.CreateLogger<WebSocketAdapter>(), client);
            
            client.Options.KeepAliveInterval = new TimeSpan(0, 0, 5);
            await client.ConnectAsync(new Uri(Uri), CancellationToken.None);
            Clients.Add(adapter);
            return adapter;
        }
    }
}