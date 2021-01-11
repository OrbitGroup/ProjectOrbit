using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TriangleCollector.Models.Exchanges.Bitstamp
{
    public class BitstampClient
    {
        public string BitstampApi { get; } = "wss://ws.bitstamp.net";

        public async Task<WebSocketAdapter> GetExchangeClient()
        {
            var baseClient = new ClientWebSocket();
            var factory = new LoggerFactory();
            var client = new WebSocketAdapter(factory.CreateLogger<WebSocketAdapter>(), baseClient);

            baseClient.Options.KeepAliveInterval = new TimeSpan(0, 0, 5);
            await baseClient.ConnectAsync(new Uri(BitstampApi), CancellationToken.None);

            return client;
        }

    }
}