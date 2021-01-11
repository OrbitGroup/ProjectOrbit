using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models.Interfaces;

namespace TriangleCollector.Models
{
    public interface IClientWebSocket
    {
        public IExchange Exchange { get; set; }
        public WebSocketState State { get; }
        public Task<WebSocketReceiveResult> ReceiveAsync(MemoryStream ms, ArraySegment<byte> buffer, CancellationToken cancellationToken);

        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool endOfMessage, CancellationToken cancellationToken);
    }
}
