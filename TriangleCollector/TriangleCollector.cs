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

        public static List<string> currencies = new List<string>();

        public static void Main(string[] args)
        {
            var httpClient = new HttpClient();

            var tickers = JsonDocument.ParseAsync(httpClient.GetStreamAsync("https://api.hitbtc.com/api/2/public/ticker").Result).Result;

            int count = 0;
            foreach (var ticker in tickers.RootElement.EnumerateArray()) 
            {
                currencies.Add(ticker.GetProperty("symbol").ToString());
                count++;
                if (count > 450)
                {
                    break;
                }
            }

            var TaskList = new List<Task>();

            foreach (var currency in currencies)
            {
                //Console.WriteLine($"Starting task for {currency}");
                TaskList.Add(Task.Run(async () => await SocketThread.MonitorOrderbooks(currency)));
                Thread.Sleep(10000); // waiting 10 seconds between each order book subscription but still getting rate limited... 
            }

            Task.WaitAll(TaskList.ToArray());
            
        }

        public class SocketThread
        {
            public static async Task MonitorOrderbooks(string symbol)
            {
                var client = new ClientWebSocket();
                int count = 0;
                try
                {

                    await client.ConnectAsync(new Uri(Uri), CancellationToken.None);
                    Thread.Sleep(10000);
                    var cts = new CancellationToken();
                    await client.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{{\"method\": \"subscribeOrderbook\",\"params\": {{ \"symbol\": \"{symbol}\" }} }}")), WebSocketMessageType.Text, true, cts);
                    Console.WriteLine($"Subscribed to {symbol}");
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    throw ex;
                }
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
                                            //Console.WriteLine($"Starting Merge for {orderbook.symbol}");
                                            count++;
                                            //Console.WriteLine($"{symbol} # of merges: {count}");
                                            OfficialOrderbooks.TryGetValue(orderbook.symbol, out Orderbook OfficialOrderbook);
                                            OfficialOrderbook.Merge(orderbook);
                                            //Console.WriteLine($"Merge took {sw.ElapsedMilliseconds}ms");

                                        }
                                        else if (orderbook.method == "snapshotOrderbook") //This is called whenever the method is not update. The first response we get is just confirmation we subscribed, second response is the "snapshot" which becomes the OfficialOrderbook
                                        {
                                            OfficialOrderbooks.AddOrUpdate(orderbook.symbol, orderbook, (key, oldValue) => oldValue = orderbook);
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
        }
    }
}
