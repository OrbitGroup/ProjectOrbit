﻿using Microsoft.Extensions.Hosting;
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

namespace TriangleCollector.Services
{
    public class OrderbookListener : BackgroundService
    {
        private readonly ILogger<OrderbookListener> _logger;

        private IClientWebSocket client;

        public OrderbookListener(ILogger<OrderbookListener> logger, IClientWebSocket client)
        {
            _logger = logger;
            this.client = client;
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
                                    var orderbookUpdateDelta = DateTime.UtcNow.Subtract(orderbook.timestamp);
                                    TriangleCollector.OrderbookUpdateDeltas.Enqueue(orderbookUpdateDelta);

                                    if (orderbook.method == "updateOrderbook") // if its an update, merge with the OfficialOrderbook
                                    {
                                        try
                                        {
                                            TriangleCollector.OfficialOrderbooks.TryGetValue(orderbook.symbol, out Orderbook OfficialOrderbook);
                                            if (OfficialOrderbook != null)
                                            {
                                                stopwatch.Reset();
                                                stopwatch.Start();
                                                var shouldRecalculate = OfficialOrderbook.Merge(orderbook);
                                                stopwatch.Stop();

                                                if (shouldRecalculate)
                                                {
                                                    if (TriangleCollector.SymbolTriangleMapping.TryGetValue(orderbook.symbol, out List<Triangle> impactedTriangles))
                                                    {
                                                        impactedTriangles.ForEach(TriangleCollector.TrianglesToRecalculate.Enqueue);
                                                    }
                                                }

                                                TriangleCollector.MergeTimings.Enqueue(stopwatch.ElapsedMilliseconds);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Error merging orderbook for symbol {orderbook.symbol}");
                                            Console.WriteLine(ex.Message);
                                        }

                                    }
                                    //This is called whenever the method is not update. The first response we get is just confirmation we subscribed, second response is the "snapshot" which becomes the OfficialOrderbook
                                    else if (orderbook.method == "snapshotOrderbook") 
                                    {
                                        TriangleCollector.OfficialOrderbooks.AddOrUpdate(orderbook.symbol, orderbook, (key, oldValue) => oldValue = orderbook);
                                        if (TriangleCollector.SymbolTriangleMapping.TryGetValue(orderbook.symbol, out List<Triangle> impactedTriangles))
                                        {
                                            impactedTriangles.ForEach(TriangleCollector.TrianglesToRecalculate.Enqueue);
                                        }
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
