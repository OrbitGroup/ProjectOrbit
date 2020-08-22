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

namespace TriangleCollector.Services
{
    public class OrderbookListener : BackgroundService
    {
        private readonly ILogger<OrderbookListener> _logger;

        private ClientWebSocket client;

        public OrderbookListener(ILogger<OrderbookListener> logger, ClientWebSocket client)
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

                    do
                    {
                        try
                        {
                            result = await client.ReceiveAsync(buffer, CancellationToken.None);
                            ms.Write(buffer.Array, buffer.Offset, result.Count);
                        }
                        catch (WebSocketException ex)
                        {
                            _logger.LogError(ex.Message);
                            _logger.LogError(ex.InnerException.Message);
                        }
                    }
                    while (!result.EndOfMessage && !stoppingToken.IsCancellationRequested);
                    ms.Seek(0, SeekOrigin.Begin);

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
                                                    TriangleCollector.UpdatedSymbols.Enqueue(orderbook.symbol);
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
                                        TriangleCollector.UpdatedSymbols.Enqueue(orderbook.symbol);
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
