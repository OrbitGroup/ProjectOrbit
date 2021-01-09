using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models;
using TriangleCollector.Models.Exchange_Models;

namespace TriangleCollector.Services
{
    public class OrderbookListener : BackgroundService
    {
        private readonly ILogger<OrderbookListener> _logger;

        private IClientWebSocket Client;

        private Exchange Exchange { get; set; }

        public OrderbookListener(ILogger<OrderbookListener> logger, IClientWebSocket client, Exchange exch)
        {
            _logger = logger;
            this.Client = client;
            Exchange = exch;
        }
        public async Task SendPong(long pong) //sends a 'pong' message back to the server if required to maintain connection
        {
            var cts = new CancellationToken();
            await Client.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{{\"pong\": {pong}}}")), WebSocketMessageType.Text, true, cts);
            Console.WriteLine($"sent back pong {pong} to {Exchange.ExchangeName}");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting Orderbook Listener...");
            stoppingToken.Register(() => _logger.LogDebug("Stopping Orderbook Listener..."));

            await Task.Run(async () =>
            {
                await BackgroundProcessing(stoppingToken);
            }, stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            var stopwatch = new Stopwatch();
            while (Client.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
            {
                if (Client.State == WebSocketState.CloseReceived)
                {
                    _logger.LogWarning("CLOSE RECEIVED");
                }
                var buffer = WebSocket.CreateClientBuffer(1024 * 64, 1024);

                WebSocketReceiveResult result = null;

                using (var ms = new MemoryStream())
                {
                    string payload = string.Empty;
                    result = await Client.ReceiveAsync(ms, buffer, CancellationToken.None);

                    if(result.MessageType == WebSocketMessageType.Text) //hitbtc, binance
                    {
                        var reader = new StreamReader(ms, Encoding.UTF8);
                        payload = await reader.ReadToEndAsync();
                    } else if (result.MessageType == WebSocketMessageType.Binary) //huobi global sends all data in a compressed GZIP format
                    {
                        var byteArray = ms.ToArray();
                        using var decompressedStream = new MemoryStream();
                        using var compressedStream = new MemoryStream(byteArray);
                        using var deflateStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                        deflateStream.CopyTo(decompressedStream);
                        decompressedStream.Position = 0;

                        using var streamReader = new StreamReader(decompressedStream);
                        payload = streamReader.ReadToEnd();                        
                    }
                    try
                    {
                        //Console.WriteLine($"payload is {payload}");
                        var orderbook = JsonSerializer.Deserialize<Orderbook>(payload); //takes a string and returns an orderbook

                        if (orderbook.Symbol != null)
                        {
                            try
                            {
                                Exchange.OfficialOrderbooks.TryGetValue(orderbook.Symbol, out Orderbook OfficialOrderbook);
                                if (OfficialOrderbook != null)
                                {
                                    bool shouldRecalculate = false;
                                    lock (OfficialOrderbook.OrderbookLock) //only lock the orderbook when the orderbook is actually being modified
                                    {
                                        shouldRecalculate = OfficialOrderbook.Merge(orderbook);
                                    }    
                                    if (shouldRecalculate)
                                    {
                                        if (Exchange.TriarbMarketMapping.TryGetValue(orderbook.Symbol, out List<Triangle> impactedTriangles)) //get all of the impacted triangles
                                        {
                                            foreach (var impactedTriangle in impactedTriangles)
                                            {
                                                Exchange.ImpactedTriangleCounter++;
                                                if ((DateTime.UtcNow - impactedTriangle.LastQueued).TotalSeconds > 1) //this triangle hasn't been queued in the last second
                                                {
                                                    Exchange.TrianglesToRecalculate.Enqueue(impactedTriangle);
                                                    impactedTriangle.LastQueued = DateTime.UtcNow;
                                                } else
                                                {
                                                    Exchange.RedundantTriangleCounter++;
                                                }
                                            }
                                        } else
                                        {
                                            Console.WriteLine("no mapped triangles corresponding to orderbook");
                                        }
                                    }
                                } else
                                {
                                    Console.WriteLine("there was no corresponding official orderbook to merge to");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error merging orderbook for market {orderbook.Symbol} on {Exchange.ExchangeName}. Websocket payload was {payload}");
                                Console.WriteLine(ex.Message);
                            }
                        } else if (orderbook.Pong == true)
                        {
                            await SendPong(orderbook.PongValue);
                            orderbook.Pong = false; //set the flag back to false
                        } else
                        {
                            //Console.WriteLine($"no orderbook symbol - payload was {payload}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"exception: {ex.Message}");
                        throw ex;
                    }
                }
            }
        }
    }
}
