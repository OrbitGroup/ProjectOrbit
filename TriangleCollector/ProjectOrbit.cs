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
using System.Net.Http;

namespace TriangleCollector
{
    public class ProjectOrbit
    {
        //public static List<Type> ExchangesToInitialize = new List<Type>() {typeof(HuobiExchange)}; 
        public static List<IExchange> Exchanges = new List<IExchange>(); //contains all exchange objects
        public static HttpClient StaticHttpClient = new HttpClient();
        public static Dictionary<int, Type> ExchangeTypes = new Dictionary<int, Type>()
        {
            {1, typeof(BinanceExchange) },
            {2, typeof(HuobiExchange) },
            {3, typeof(HitbtcExchange) }
        };

        public static void Main(string[] args)
        {
            try
            {
                int exchangeNumber;
                Console.WriteLine("Enter the exchange number you'd like to monitor. Valid exchange options are \n1: Binance\n2: Huobi\n3: Hitbtc");
                exchangeNumber = int.Parse(Console.ReadLine());
                InitializeExchanges(exchangeNumber); 
                CreateHostBuilder(args).Build().Run(); 
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
                services.AddHostedService<ExchangeServiceInitializer>();
                services.AddHostedService<USDMonitor>();
                services.AddApplicationInsightsTelemetryWorkerService();
            });

       
        public static void InitializeExchanges(int exch)
        {
            var exchangeType = ExchangeTypes[exch];
            IExchange exchange = (IExchange)Activator.CreateInstance(exchangeType, exchangeType.Name);
            Exchanges.Add(exchange);   
        }
    }
}