﻿using Microsoft.ApplicationInsights;
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
using TriangleCollector.Models.Interfaces;

namespace TriangleCollector.Services
{
    public class OrderbookListener : BackgroundService
    {
        private readonly ILogger<OrderbookListener> _logger;
        private readonly TelemetryClient _telemetryClient;

        private int ID { get; set; }

        private IClientWebSocket Client;

        private IExchange Exchange { get; set; }

        private Type OrderbookType { get; set; }
        private CancellationToken StoppingToken { get; set; }

        public OrderbookListener(ILogger<OrderbookListener> logger, TelemetryClient telemetryClient, IClientWebSocket client, IExchange exch, int id)
        {
            _logger = logger;
            _telemetryClient = telemetryClient;
            Client = client;
            Exchange = exch;
            ID = id;
            OrderbookType = Exchange.OrderbookType;
            Client.Exchange = Exchange;
        }
        

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug($"Starting Orderbook Listener {ID} for {Exchange.ExchangeName}...");
            stoppingToken.Register(() => _logger.LogDebug($"Stopping Orderbook Listener {ID} for {Exchange.ExchangeName}..."));
            StoppingToken = stoppingToken;

            await Task.Run(async () =>
            {
                await BackgroundProcessing(stoppingToken);
            }, stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            var dataReceiptMetric = _telemetryClient.GetMetric("DataReceipt", "Category");

            while (Client.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
            {
                var buffer = WebSocket.CreateClientBuffer(1024 * 64, 1024);

                using (var ms = new MemoryStream())
                {
                    string payload = string.Empty;
                    WebSocketReceiveResult result = new WebSocketReceiveResult(0, WebSocketMessageType.Text, false);

                    result = await Client.ReceiveAsync(ms, buffer, CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text) //hitbtc, binance
                    {
                        var reader = new StreamReader(ms, Encoding.UTF8);
                        payload = await reader.ReadToEndAsync();
                    } 
                    else if (result.MessageType == WebSocketMessageType.Binary) //huobi global sends all data in a compressed GZIP format
                    {
                        payload = ReadBinarySocketMessage(ms);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogError($"{Exchange.ExchangeName}: Socket connection closed because {result.CloseStatusDescription}");
                        await HandleWebsocketClose();
                        continue;
                    }

                    IOrderbook orderbook = (IOrderbook)JsonSerializer.Deserialize(payload, OrderbookType);

                    if(orderbook.Pong == true)
                    {
                        SendPong(orderbook); //do not await this task; the listener should move on in reading other websocket messages
                        continue;
                    }

                    if(Exchange.OfficialOrderbooks.TryGetValue(orderbook.Symbol, out IOrderbook OfficialOrderbook))
                    {
                        (bool IsSignificant, string Category) shouldRecalculate;
                        lock (OfficialOrderbook.OrderbookLock) //lock the orderbook when the orderbook is being modified
                        {
                            shouldRecalculate = OfficialOrderbook.Merge(orderbook);
                        }

                        dataReceiptMetric.TrackValue(1, shouldRecalculate.Category);
                        dataReceiptMetric.TrackValue(1, "Total");

                        if (shouldRecalculate.IsSignificant)
                        {
                            if (Exchange.TriangleTemplates.TryGetValue(orderbook.Symbol, out List<Triangle> impactedTriangles)) //get all of the impacted triangles
                            {
                                foreach (var impactedTriangle in impactedTriangles)
                                {
                                    //create a new Triangle object from the TriangleTemplates list to avoid threading issues
                                    var triangle = new Triangle(impactedTriangle.FirstSymbolOrderbook, impactedTriangle.SecondSymbolOrderbook, impactedTriangle.ThirdSymbolOrderbook, impactedTriangle.Direction, impactedTriangle.Exchange);
                                    CreateSnapshots.CreateOrderbookSnapshots(triangle);
                                    Exchange.TrianglesToRecalculate.Enqueue(triangle);
                                }
                            }
                        }
                    } 
                }
            }
            await HandleWebsocketClose();
        }

        public async Task SendPong(IOrderbook orderbook) //sends a 'pong' message back to the server if required to maintain connection. Only Huobi (so far) uses this methodology
        {
            if (Client.State == WebSocketState.Open)
            {
                try
                {
                    var cts = new CancellationToken();
                    await Client.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{{\"pong\": {orderbook.PongValue}}}")), WebSocketMessageType.Text, true, cts);
                    //_logger.LogDebug($"took {(DateTime.UtcNow - orderbook.Timestamp).TotalMilliseconds}ms to send pong");
                    orderbook.Pong = false; //set the flag back to false   
                }
                catch (Exception ex)
                {
                    _logger.LogError($"problem sending pong: {ex.Message}");
                }
            }
        }
        public string ReadBinarySocketMessage(MemoryStream ms)
        {
            var byteArray = ms.ToArray();
            using var decompressedStream = new MemoryStream();
            using var compressedStream = new MemoryStream(byteArray);
            using var deflateStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            deflateStream.CopyTo(decompressedStream);
            decompressedStream.Position = 0;

            using var streamReader = new StreamReader(decompressedStream);
            return(streamReader.ReadToEnd());
        }
        public async Task HandleWebsocketClose()
        {
            _logger.LogError($"ClientDisconnected");
            Console.WriteLine($"Client disconnected at {DateTime.Now}");
            Exchange.ActiveClients.Remove(Client);
            Exchange.InactiveClients.Add(Client);
            Exchange.AggregateStreamOpen = false;
            foreach(var market in Client.Markets)
            {
                Exchange.SubscribedMarkets.TryRemove(market.Symbol, out var _);
                Exchange.SubscriptionQueue.Enqueue(market);
            }
            await StopAsync(StoppingToken);
        }
    }
}
