using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace TriangleCollector.Models
{
    public interface IClientWebSocket
    {
        public WebSocketState State { get; }
        public Task<WebSocketReceiveResult> ReceiveAsync(MemoryStream ms, ArraySegment<byte> buffer, CancellationToken cancellationToken);

        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool endOfMessage, CancellationToken cancellationToken);
    }
}
