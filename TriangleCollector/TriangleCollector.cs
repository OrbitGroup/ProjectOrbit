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

        public static ConcurrentDictionary<string, decimal> Triangles = new ConcurrentDictionary<string, decimal>();

        public static ConcurrentDictionary<string, List<Triangle>> SymbolTriangleMapping = new ConcurrentDictionary<string, List<Triangle>>();

        public static List<string> Pairs = new List<string>();

        public static HashSet<string> BaseCoins = new HashSet<string>();

        public static HashSet<string> AltCoins = new HashSet<string>();

        public static ConcurrentBag<string> ActiveSymbols = new ConcurrentBag<string>();

        public static ConcurrentQueue<string> UpdatedSymbols = new ConcurrentQueue<string>();

        public static ConcurrentQueue<Triangle> TrianglesToRecalculate = new ConcurrentQueue<Triangle>();

        public static ConcurrentQueue<Triangle> RecalculatedTriangles = new ConcurrentQueue<Triangle>();

        public static ClientWebSocket client = new ClientWebSocket();

        public IConfiguration Configuration { get; }

        public static async Task Main(string[] args)
        {
            client.Options.KeepAliveInterval = new TimeSpan(0, 0, 5);
            await client.ConnectAsync(new Uri(Uri), CancellationToken.None);


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
                services.AddHostedService<OrderbookListener>();
                services.AddHostedService<TriangleCalculator>();
            });

        public static async Task MonitorUpdatedTriangles()
        {
            //get triangle:profit from Triangles dict
            //push to redis
        }

    }
}
