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

        private IClientWebSocket client;

        private Exchange exchange { get; set; }

        public OrderbookListener(ILogger<OrderbookListener> logger, IClientWebSocket client, Exchange exch)
        {
            _logger = logger;
            this.client = client;
            exchange = exch;
        }
        public async Task sendPong(long pong) //sends a 'pong' message back to the server if required to maintain connection
        {
            var cts = new CancellationToken();
            await client.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{{\"pong\": {pong}}}")), WebSocketMessageType.Text, true, cts);
            Console.WriteLine($"sent back pong {pong} to {exchange.exchangeName}");
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
            while (client.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
            {
                if (client.State == WebSocketState.CloseReceived)
                {
                    _logger.LogWarning("CLOSE RECEIVED");
                }
                var buffer = WebSocket.CreateClientBuffer(1024 * 64, 1024);

                WebSocketReceiveResult result = null;

                using (var ms = new MemoryStream())
                {
                    string payload = string.Empty;
                    result = await client.ReceiveAsync(ms, buffer, CancellationToken.None);

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

                        if (orderbook.symbol != null)
                        {
                            try
                            {
                                exchange.OfficialOrderbooks.TryGetValue(orderbook.symbol, out Orderbook OfficialOrderbook);
                                if (OfficialOrderbook != null)
                                {
                                    bool shouldRecalculate = false;
                                    lock (OfficialOrderbook.orderbookLock) //only lock the orderbook when the orderbook is actually being modified
                                    {
                                        shouldRecalculate = OfficialOrderbook.Merge(orderbook);
                                    }    
                                    if (shouldRecalculate)
                                    {
                                        if (exchange.triarbMarketMapping.TryGetValue(orderbook.symbol, out List<Triangle> impactedTriangles)) //get all of the impacted triangles
                                        {
                                            foreach (var impactedTriangle in impactedTriangles)
                                            {
                                                exchange.impactedTriangleCounter++;
                                                if ((DateTime.UtcNow - impactedTriangle.lastQueued).TotalSeconds > 1) //this triangle hasn't been queued in the last second
                                                {
                                                    exchange.TrianglesToRecalculate.Enqueue(impactedTriangle);
                                                    impactedTriangle.lastQueued = DateTime.UtcNow;
                                                } else
                                                {
                                                    exchange.redundantTriangleCounter++;
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
                                Console.WriteLine($"Error merging orderbook for market {orderbook.symbol} on {exchange.exchangeName}. Websocket payload was {payload}");
                                Console.WriteLine(ex.Message);
                            }
                        } else if (orderbook.pong == true)
                        {
                            await sendPong(orderbook.pongValue);
                            orderbook.pong = false; //set the flag back to false
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
