using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models;

namespace TriangleCollector.UnitTests.Models
{
    class MockWebSocketAdapter : IClientWebSocket
    {
        public WebSocketState State => WebSocketState.Open;

        public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {

            return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Text, true));
        }

        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool endOfMessage, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
