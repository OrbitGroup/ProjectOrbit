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

        private int ID { get; set; }

        private IClientWebSocket Client;

        private IExchange Exchange { get; set; }

        private Type OrderbookType { get; set; }

        public OrderbookListener(ILogger<OrderbookListener> logger, IClientWebSocket client, IExchange exch, int id)
        {
            _logger = logger;
            Client = client;
            Exchange = exch;
            ID = id;
            OrderbookType = Exchange.OrderbookType;
            Client.Exchange = Exchange;
        }
        public async Task SendPong(IOrderbook orderbook) //sends a 'pong' message back to the server if required to maintain connection. Only Huobi (so far) uses this methodology
        {
            if(Client.State == WebSocketState.Open)
            {
                var cts = new CancellationToken();
                await Client.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{{\"pong\": {orderbook.PongValue}}}")), WebSocketMessageType.Text, true, cts);
                //_logger.LogDebug($"took {(DateTime.UtcNow - orderbook.Timestamp).TotalMilliseconds}ms to send pong");
                orderbook.Pong = false; //set the flag back to false   
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug($"Starting Orderbook Listener {ID} for {Exchange.ExchangeName}...");
            stoppingToken.Register(() => _logger.LogDebug($"Stopping Orderbook Listener {ID} for {Exchange.ExchangeName}..."));

            await Task.Run(async () =>
            {
                await BackgroundProcessing(stoppingToken);
            }, stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (Client.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
            {
                if (Client.State == WebSocketState.CloseReceived)
                {
                    _logger.LogWarning("CLOSE RECEIVED");
                }
                var buffer = WebSocket.CreateClientBuffer(1024 * 640, 1024);

                using (var ms = new MemoryStream())
                {
                    string payload = string.Empty;
                    WebSocketReceiveResult result = await Client.ReceiveAsync(ms, buffer, CancellationToken.None);

                    if(result.MessageType == WebSocketMessageType.Text) //hitbtc, binance
                    {
                        var reader = new StreamReader(ms, Encoding.UTF8);
                        payload = await reader.ReadToEndAsync();
                    } 
                    else if (result.MessageType == WebSocketMessageType.Binary) //huobi global sends all data in a compressed GZIP format
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
                        if (payload == "")
                        {
                            //_logger.LogWarning("Blank payload");
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                _logger.LogError($"Socket connection closed because: {result.CloseStatusDescription.ToString()}");
                                await Stop();
                            }
                            continue;
                        }

                        IOrderbook orderbook = (IOrderbook)JsonSerializer.Deserialize(payload, OrderbookType);

                        if (orderbook.Symbol != null)
                        {
                            try
                            {
                                if(Exchange.OfficialOrderbooks.TryGetValue(orderbook.Symbol, out IOrderbook OfficialOrderbook))
                                {
                                    bool shouldRecalculate = false;
                                    lock (OfficialOrderbook.OrderbookLock) //lock the orderbook when the orderbook is being modified
                                    {
                                        shouldRecalculate = OfficialOrderbook.Merge(orderbook);
                                    }

                                    if (shouldRecalculate)
                                    {
                                        if (Exchange.TriarbMarketMapping.TryGetValue(orderbook.Symbol, out List<Triangle> impactedTriangles)) //get all of the impacted triangles
                                        {
                                            foreach (var impactedTriangle in impactedTriangles)
                                            {
                                                if (Exchange.TrianglesToRecalculate.Contains(impactedTriangle) == false) //this triangle isn't already in the queue
                                                {
                                                    Exchange.TrianglesToRecalculate.Enqueue(impactedTriangle);
                                                }
                                            }
                                        }
                                    }
                                }                                
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Error merging orderbook for market {orderbook.Symbol} on {Exchange.ExchangeName}. Websocket payload was {payload}");
                                _logger.LogError(ex.Message);
                            }
                        } 
                        else if (orderbook.Pong == true)
                        {
                            try
                            {
                                SendPong(orderbook); //do not await this task; the listener should move on in reading other websocket messages
                            } catch(Exception ex)
                            {
                                _logger.LogError($"problem sending pong: {ex.Message}");
                                await Stop();
                            }  
                        } 
                    }
                    catch (Exception ex)
                    {
                        if (ex.InnerException.Message == "An established connection was aborted by the software in your host machine." || ex.InnerException.Message == "Unable to read data from the transport connection: An established connection was aborted by the software in your host machine..")
                        {
                            _logger.LogError($"Connection aborted on {Exchange.ExchangeName}");
                            await Stop();
                        }
                        else
                        {
                            _logger.LogError($"exception related to {payload}");
                            _logger.LogError($"exception: {ex.Message}");
                        }
                        throw ex;
                    }
                }
            }
            await Stop();
        }
        private async Task Stop()
        {
            _logger.LogWarning($"client aborted on {Exchange} with {Client.Markets.Count} subscribed markets. Queuing lost markets for re-subscription");
            foreach (var market in Client.Markets)
            {
                Exchange.SubscribedMarkets.TryRemove(market.Symbol, out var _);
                Exchange.SubscriptionQueue.Enqueue(market);
            }
        }
    }
}
