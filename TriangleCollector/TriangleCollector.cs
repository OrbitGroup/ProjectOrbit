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
using TriangleCollector.Models.Exchange_Models;

namespace TriangleCollector
{
    public class TriangleCollector
    {
        public static ConcurrentDictionary<string, Triangle> Triangles = new ConcurrentDictionary<string, Triangle>(); //delete once QueueMonitor refactored

        public static ConcurrentDictionary<string, DateTime> TriangleRefreshTimes = new ConcurrentDictionary<string, DateTime>();

        public static ConcurrentDictionary<string, int> ProfitableSymbolMapping = new ConcurrentDictionary<string, int>(); //this can stay here for now, but ultimately will reside within the exchange objects

        public static ConcurrentQueue<Triangle> TrianglesToRecalculate = new ConcurrentQueue<Triangle>(); //delete once QueueMonitor refactored

        public static ConcurrentQueue<Triangle> RecalculatedTriangles = new ConcurrentQueue<Triangle>();

        public static ConcurrentQueue<long> MergeTimings = new ConcurrentQueue<long>(); //I don't think this is needed?

        public static ConcurrentQueue<TimeSpan> OrderbookUpdateDeltas = new ConcurrentQueue<TimeSpan>(); //I don't think this is needed?

        public static List<String> exchangeList = new List<String>() { "hitbtc" };//, "binance" }; 
        public static List<Exchange> exchanges = new List<Exchange>();
        public static ExchangeAPI restAPIs = new ExchangeAPI();


        //TO-DO: have these counters either included in the exchange objects or in another object 
        public static double allOrderBookCounter = 0;

        public static double InsideLayerCounter = 0;

        public static double OutsideLayerCounter = 0;

        public static double PositivePriceChangeCounter = 0;

        public static double NegativePriceChangeCounter = 0;


        public static void Main(string[] args)
        {
            initializeExchanges(); //exchange objects are initialized synchronously and their constructors map out every possible triangular arbitrage trade. 
            CreateHostBuilder(args).Build().Run(); //the orderbooksubscriber service then references those mapped markets 
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
                //services.AddHostedService<QueueMonitor>(); one is started for each exchange
                services.AddHostedService<OrderbookSubscriber>(); //this is the only service that starts standalone
                //services.AddHostedService<TriangleCalculator>(); one is started for each exchange
                //services.AddHostedService<TrianglePublisher>();
            });

       
        public static void initializeExchanges()
        {
            foreach(string exchangeName in exchangeList)
            {
                var exchange = new Exchange(exchangeName);
                exchanges.Add(exchange);
            }
        }
    }
}