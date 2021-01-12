using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TriangleCollector.Services;
using TriangleCollector.Models.Exchanges.Binance;
using TriangleCollector.Models.Interfaces;
using TriangleCollector.Models.Exchanges.Hitbtc;
using TriangleCollector.Models.Exchanges.Huobi;

namespace TriangleCollector
{
    public class TriangleCollector
    {
        public static ConcurrentQueue<long> MergeTimings = new ConcurrentQueue<long>(); //I don't think this is needed?

        public static ConcurrentQueue<TimeSpan> OrderbookUpdateDeltas = new ConcurrentQueue<TimeSpan>(); //I don't think this is needed?

        public static List<Type> ExchangesToInitialize = new List<Type>() { typeof(HuobiExchange)}; //list of exchanges to initialize. Valid names are 'hitbtc', 'huobi', and 'binance'
        public static List<IExchange> Exchanges = new List<IExchange>(); //contains all exchange objects

        public static void Main(string[] args)
        {
            try
            {
                InitializeExchanges(); //exchange objects are initialized synchronously and their constructors map out every possible triangular arbitrage trade. 
                CreateHostBuilder(args).Build().Run(); //the orderbooksubscriber service then references those mapped markets 
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("TriangleCollector Stopped");
            }
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
                services.AddHostedService<USDMonitor>();
                services.AddHostedService<OrderbookSubscriber>();
                services.AddHostedService<ActivityMonitor>();
                //services.AddHostedService<TriangleCalculator>(); started for each exchange
                //services.AddHostedService<TrianglePublisher>();
            });

       
        public static void InitializeExchanges()
        {
            foreach(var exchangeName in ExchangesToInitialize)
            {
                IExchange exchange = (IExchange)Activator.CreateInstance(exchangeName, exchangeName.ToString());
                Exchanges.Add(exchange);
            }
        }
    }
}