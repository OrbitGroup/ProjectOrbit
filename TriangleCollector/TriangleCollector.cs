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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TriangleCollector.Models;

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



        public static async Task Main(string[] args)
        {
            var httpClient = new HttpClient();

            var symbols = JsonDocument.ParseAsync(httpClient.GetStreamAsync("https://api.hitbtc.com/api/2/public/symbol").Result).Result;
            
            httpClient.Dispose();


            foreach (var symbol in symbols.RootElement.EnumerateArray()) 
            {
                Pairs.Add(symbol.GetProperty("id").ToString());
                BaseCoins.Add(symbol.GetProperty("quoteCurrency").ToString());
                AltCoins.Add(symbol.GetProperty("baseCurrency").ToString());
            }

            BaseCoins.Remove("BTC"); //BTC is implied

            GetTrianglesFromSymbols();

            var TaskList = new List<Task>();

            await client.ConnectAsync(new Uri(Uri), CancellationToken.None);
            
            await Task.Run(async () => await SocketThread.SubscribeOrderbooks());
            TaskList.Add(Task.Run(async () => await SocketThread.ListenOrderbooks()));
            TaskList.Add(Task.Run(async () => await SocketThread.QueueStatistics()));
            TaskList.Add(Task.Run(async () => await SocketThread.MonitorUpdatedSymbols()));
            TaskList.Add(Task.Run(async () => await SocketThread.MonitorTrianglesToRecalculate()));
            

            Task.WaitAll(TaskList.ToArray());

        }

        public static void GetTrianglesFromSymbols()
        {
            var triangles = new List<Triangle>();

            var altBtc = Pairs.Where(x => x.EndsWith("BTC")).ToList();
            var altBase = Pairs.Where(x => BaseCoins.Any(x.Contains) && !x.EndsWith("BTC")).ToList();
            var baseBtc = Pairs.Where(x => x.EndsWith("BTC")).ToList();

            foreach (var firstPair in altBtc)
            {
                foreach (var secondPair in altBase)
                {
                    foreach ( var thirdPair in baseBtc)
                    {
                        var firstPairAlt = "INVALID";
                        if (firstPair.EndsWith("BTC"))
                        {
                            firstPairAlt = firstPair.Remove(firstPair.Length - 3);
                        }

                        var secondPairBase = "INVALID";
                        if (secondPair.StartsWith(firstPairAlt))
                        {
                            secondPairBase = secondPair.Remove(0, firstPairAlt.Length);
                            if (!BaseCoins.Contains(secondPairBase)) secondPairBase = "INVALID";
                        }

                        if (secondPair.Contains(firstPairAlt) && thirdPair.Contains(secondPairBase) && thirdPair.Length == secondPairBase.Length + 3)
                        {
                            var newTriangle = new Triangle(firstPair, secondPair, thirdPair);

                            foreach (var pair in new List<string> {firstPair, secondPair, thirdPair })
                            {
                                SymbolTriangleMapping.AddOrUpdate(pair, new List<Triangle>() { newTriangle }, (key, triangleList) =>
                                {
                                    if (key == pair)
                                    {
                                        triangleList.Add(newTriangle);
                                    }
                                    return triangleList;
                                });
                            }
                        }
                    }
                }
            }
        }

        public class SocketThread
        {
            public static async Task SubscribeOrderbooks()
            {
                foreach (var symbol in Pairs)
                {
                    try
                    {
                        var cts = new CancellationToken();
                        await client.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{{\"method\": \"subscribeOrderbook\",\"params\": {{ \"symbol\": \"{symbol}\" }} }}")), WebSocketMessageType.Text, true, cts);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        throw ex;
                    }
                }
            }

            public static async Task ListenOrderbooks() 
            {
                while (client.State == WebSocketState.Open)
                {
                    ArraySegment<Byte> buffer = new ArraySegment<byte>(new Byte[8192]);

                    WebSocketReceiveResult result = null;

                    using (var ms = new MemoryStream())
                    {

                        do
                        {
                            result = await client.ReceiveAsync(buffer, CancellationToken.None);
                            ms.Write(buffer.Array, buffer.Offset, result.Count);
                        }
                        while (!result.EndOfMessage);
                        
                        ms.Seek(0, SeekOrigin.Begin);

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            using (var reader = new StreamReader(ms, Encoding.UTF8))
                            {
                                try
                                {
                                    var orderbook = JsonSerializer.Deserialize<Orderbook>(await reader.ReadToEndAsync());
                                    if (orderbook != null)
                                    {
                                        if (orderbook.method == "updateOrderbook") // if its an update, merge with the OfficialOrderbook
                                        {
                                            try
                                            {
                                                OfficialOrderbooks.TryGetValue(orderbook.symbol, out Orderbook OfficialOrderbook);
                                                if (OfficialOrderbook != null)
                                                {
                                                    var delta = DateTime.UtcNow.Subtract(orderbook.timestamp);
                                                    if (delta.TotalSeconds > 1)
                                                    {
                                                        Console.WriteLine($"Now: {DateTime.UtcNow} | Orderbook: {orderbook.timestamp}");
                                                        Console.WriteLine($"{orderbook.symbol} delta: {delta.TotalSeconds}");
                                                    }
                                                    OfficialOrderbook.Merge(orderbook);
                                                    UpdatedSymbols.Enqueue(orderbook.symbol);
                                                    
                                                }
                                                else
                                                {
                                                    Console.WriteLine($"Orderbook miss for {orderbook.symbol}");
                                                }
                                            }
                                            catch(Exception ex)
                                            {
                                                Console.WriteLine($"Error merging orderbook for symbol {orderbook.symbol}");
                                                Console.WriteLine(ex.Message);
                                            }

                                        }
                                        else if (orderbook.method == "snapshotOrderbook") //This is called whenever the method is not update. The first response we get is just confirmation we subscribed, second response is the "snapshot" which becomes the OfficialOrderbook
                                        {
                                            OfficialOrderbooks.AddOrUpdate(orderbook.symbol, orderbook, (key, oldValue) => oldValue = orderbook);
                                            UpdatedSymbols.Enqueue(orderbook.symbol);
                                        }

                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.Message);
                                    throw ex;
                                }
                            }
                        }
                    }
                }
            }

            public static async Task MonitorUpdatedSymbols()
            {
                //get updatedSymbols
                //get impacted triangles
                //push triangles to trianglesToRecalculate
                while (client.State == WebSocketState.Open)
                {
                    if (UpdatedSymbols.TryDequeue(out string symbol) && SymbolTriangleMapping.TryGetValue(symbol, out List<Triangle> impactedTriangles))
                    {
                        impactedTriangles.ForEach(TrianglesToRecalculate.Enqueue);
                    }
                }

            }

            public static async Task MonitorTrianglesToRecalculate()
            {
                //get orderbooks
                //calculate profit
                //push triangle:profit to Triangles
                //push triangle name to UpdatedTriangles
                while (client.State == WebSocketState.Open)
                {
                    if (TrianglesToRecalculate.TryDequeue(out Triangle triangle))
                    {
                        OfficialOrderbooks.TryGetValue(triangle.FirstSymbol, out Orderbook firstSymbolOrderbook);
                        OfficialOrderbooks.TryGetValue(triangle.SecondSymbol, out Orderbook secondSymbolOrderbook);
                        OfficialOrderbooks.TryGetValue(triangle.ThirdSymbol, out Orderbook thirdSymbolOrderbook);

                        if (firstSymbolOrderbook != null)
                        {
                            triangle.FirstSymbolAsk = firstSymbolOrderbook.asks.Keys.OrderBy(price => price).First();
                        }

                        if (secondSymbolOrderbook !=null)
                        {
                            triangle.SecondSymbolAsk = secondSymbolOrderbook.asks.Keys.OrderBy(price => price).First();
                            triangle.SecondSymbolBid = secondSymbolOrderbook.bids.Keys.OrderBy(price => price).First();
                        }

                        if (thirdSymbolOrderbook != null)
                        {
                            if( triangle.FirstSymbolAsk == 0)
                            {
                                break;
                            }

                            triangle.ThirdSymbolBid = thirdSymbolOrderbook.bids.Keys.OrderBy(price => price).First();
                            var profit = triangle.GetProfitability();
                            //var reversedProfit = triangle.GetReversedProfitability();
                            Triangles.AddOrUpdate(triangle.ToString(), profit, (key, oldValue) => oldValue = profit);
                        }

                        
                    }
                }
            }

            public static async Task MonitorUpdatedTriangles()
            {
                //get triangle:profit from Triangles dict
                //push to redis
            }

            public static async Task QueueStatistics()
            {
                //provide logging and queue depth monitoring
                while (client.State == WebSocketState.Open)
                {
                    Thread.Sleep(2500);
                    Console.WriteLine($"--Update--");
                    Console.WriteLine($"Orderbooks: {OfficialOrderbooks.Count}");
                    Console.WriteLine($"Triangles: {Triangles.Count}");
                    Console.WriteLine($"UpdatedSymbols: {UpdatedSymbols.Count}");
                    Console.WriteLine($"TrianglesToRecalc: {TrianglesToRecalculate.Count}");
                    foreach (var triangle in Triangles)
                    {
                        if (triangle.Value > 0)
                        {
                            Console.WriteLine($"{triangle.Key} : {triangle.Value}");
                        }
                    }
                    Console.WriteLine($"----------");
                    Thread.Sleep(2500);
                }
            }
        }
    }
}
