﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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

        public OrderbookListener(ILogger<OrderbookListener> logger)
        {
            _logger = logger;
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
            while (TriangleCollector.client.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
            {
                if (TriangleCollector.client.State == WebSocketState.CloseReceived)
                {
                    _logger.LogWarning("CLOSE RECEIVED");
                }
                ArraySegment<Byte> buffer = new ArraySegment<byte>(new byte[4096]);

                WebSocketReceiveResult result = null;

                using (var ms = new MemoryStream())
                {

                    do
                    {
                        try
                        {
                            result = await TriangleCollector.client.ReceiveAsync(buffer, CancellationToken.None);
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
                                if (orderbook != null)
                                {
                                    if (orderbook.method == "updateOrderbook") // if its an update, merge with the OfficialOrderbook
                                    {
                                        try
                                        {
                                            TriangleCollector.OfficialOrderbooks.TryGetValue(orderbook.symbol, out Orderbook OfficialOrderbook);
                                            if (OfficialOrderbook != null)
                                            {
                                                OfficialOrderbook.Merge(orderbook);
                                                TriangleCollector.UpdatedSymbols.Enqueue(orderbook.symbol);

                                            }
                                            else
                                            {
                                                Console.WriteLine($"Orderbook miss for {orderbook.symbol}");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Error merging orderbook for symbol {orderbook.symbol}");
                                            Console.WriteLine(ex.Message);
                                        }

                                    }
                                    else if (orderbook.method == "snapshotOrderbook") //This is called whenever the method is not update. The first response we get is just confirmation we subscribed, second response is the "snapshot" which becomes the OfficialOrderbook
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
