using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
                    result = await client.ReceiveAsync(ms, buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using (var reader = new StreamReader(ms, Encoding.UTF8))
                        {
                            try
                            {   
                                var orderbook = JsonSerializer.Deserialize<Orderbook>(await reader.ReadToEndAsync());

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
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Error merging orderbook for symbol {orderbook.symbol}");
                                        Console.WriteLine(ex.Message);
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
