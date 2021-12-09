using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models.Interfaces;

namespace TriangleCollector.Models
{
    public class WebSocketAdapter : IClientWebSocket
    {
        private readonly ILogger<WebSocketAdapter> _logger;
        private readonly ClientWebSocket _client;
        public DateTime TimeStarted { get; set; }
        public List<IOrderbook> Markets { get; set; } //tracks the markets subscribed on each websocket client

        public WebSocketAdapter(ILogger<WebSocketAdapter> logger, ClientWebSocket client)
        {
            _logger = logger;
            _client = client;
        }

        public WebSocketState State => _client.State;

        public IExchange Exchange { get; set; }

        public async Task<WebSocketReceiveResult> ReceiveAsync(MemoryStream ms, ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            WebSocketReceiveResult result = new WebSocketReceiveResult(0, WebSocketMessageType.Text, true);

            do
            {
                try
                {
                    result = await _client.ReceiveAsync(buffer, CancellationToken.None);
                    if(result != null)
                    {
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
                    } 
                    else
                    {
                        var closeResult = new WebSocketReceiveResult(1, WebSocketMessageType.Close, true);
                        return (closeResult);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"WebSocket Client disconnected: {_client.CloseStatusDescription}");
                    _logger.LogError(ex.Message);
                }
            }
            while (!result.EndOfMessage && !cancellationToken.IsCancellationRequested);
            ms.Seek(0, SeekOrigin.Begin);
            return result;
        }

        public async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool endOfMessage, CancellationToken cancellationToken)
        {
            await _client.SendAsync(buffer, type, endOfMessage, cancellationToken);
        }
    }
}
