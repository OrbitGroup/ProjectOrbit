using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TriangleCollector.Models.Interfaces
{
    public interface IExchangeClient
    {
        public IExchange Exchange { get; set; }
        public string SymbolsRestApi { get; }
        public string PricesRestApi { get; }
        public string SocketClientApi { get; }
        public JsonElement.ArrayEnumerator Tickers { get; }
        public IClientWebSocket Client { get; }
        public int MaxMarketsPerClient { get; }

        public HashSet<IOrderbook> GetMarketsViaRestApi();

        public Task<WebSocketAdapter> CreateExchangeClientAsync(); //establishes initial connection to exchange for websocket

        public Task SubscribeViaQueue(IOrderbook market);

        public Task SubscribeViaAggregate();
    }
}
