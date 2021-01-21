using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TriangleCollector.Services;
using TriangleCollector.Models.Exchanges.Binance;
using TriangleCollector.Models.Interfaces;
using TriangleCollector.Models.Exchanges.Hitbtc;
using TriangleCollector.Models.Exchanges.Huobi;

namespace TriangleCollector
{
    public class ProjectOrbit
    {
        public static List<Type> ExchangesToInitialize = new List<Type>() { typeof(BinanceExchange), typeof(HuobiExchange), typeof(HitbtcExchange)}; 
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
                Console.WriteLine("ProjectOrbit Stopped");
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
                services.AddHostedService<USDMonitor>();
                services.AddHostedService<OrderbookSubscriber>();
                services.AddHostedService<ActivityMonitor>();
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