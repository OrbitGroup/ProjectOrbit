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
        public static Dictionary<string, Type> ExchangeTypes = new Dictionary<string, Type>()
        {
            {"Binance", typeof(BinanceExchange) },
            {"Huobi", typeof(HuobiExchange) },
            {"Hitbtc", typeof(HitbtcExchange) }
        };
        public static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Enter the exchange you'd like to monitor. Valid exchange names are 'Binance', 'Huobi', and 'Hitbtc'");
                var exchange = Console.ReadLine();
                InitializeExchanges(exchange); 
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
            });

       
        public static void InitializeExchanges(string exch)
        {
            var exchangeType = ExchangeTypes[exch];
            IExchange exchange = (IExchange)Activator.CreateInstance(exchangeType, exchangeType.ToString());
            Exchanges.Add(exchange);   
        }
    }
}