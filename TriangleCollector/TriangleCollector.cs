using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        public static Orderbook OfficialOrderbook;

        public static async Task Main(string[] args)
        {
            Stopwatch sw;
            var client = new ClientWebSocket();
            var cts = new CancellationToken();
            await client.ConnectAsync(new Uri(Uri), CancellationToken.None);
            
            await client.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes(@"{""method"": ""subscribeOrderbook"",""params"": { ""symbol"": ""ETHBTC"" }}")), WebSocketMessageType.Text, true, cts);
            
            
            //await client.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes(@"{""method"": ""subscribeOrderbook"",""params"": { ""symbol"": ""LTCETH"" }}")), WebSocketMessageType.Text, true, cts);
            //await client.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes(@"{""method"": ""subscribeOrderbook"",""params"": { ""symbol"": ""LTCBTC"" }}")), WebSocketMessageType.Text, true, cts);
            
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

                                var orderbook = JsonSerializer.Deserialize<Orderbook>(reader.ReadToEnd());
                                if (orderbook != null)
                                {
                                    if (orderbook.method == "updateOrderbook") // if its an update, merge with the OfficialOrderbook
                                    {
                                        Console.WriteLine("Starting Merge");
                                        sw = Stopwatch.StartNew();
                                        OfficialOrderbook.Merge(orderbook);
                                        sw.Stop();
                                        Console.WriteLine($"Merge took {sw.ElapsedMilliseconds}ms");

                                    }
                                    else //This is called whenever the method is not update. The first response we get is just confirmation we subscribed, second response is the "snapshot" which becomes the OfficialOrderbook
                                    {
                                        OfficialOrderbook = orderbook;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                throw ex;
                            }
                        }
                    }
                }
            }
        }
    }
}
