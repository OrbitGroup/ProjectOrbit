using System;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.IO;
using System.Text.Json;
using TriangleCollector.Models;
using TriangleCollector.Models.Exchanges.Bitstamp;

namespace TriangleCollector.Services
{
    public class USDMonitor: BackgroundService // the purpose of this class is to independently store the exchange rate of BTC/USD so that other services can use it to convert metrics from BTC to USD if desired. Bitstamp has a simple websocket to accomplish this.
    {
        private readonly ILogger<USDMonitor> _logger;

        public static decimal BTCUSDPrice = 0;

        public USDMonitor(ILogger<USDMonitor> logger)
        {
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting USD Monitor...");

            stoppingToken.Register(() => _logger.LogDebug("Stopping USD Monitor..."));
            await Task.Run(async () =>
            {
                await BackgroundProcessing(stoppingToken);
            }, stoppingToken);
        }
        public async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            var client = await new BitstampClient().GetExchangeClient();
            var cts = new CancellationToken();
            await client.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes($"{{ \"event\": \"bts:subscribe\", \"data\": {{\"channel\": \"live_trades_btcusd\"}} }}")), WebSocketMessageType.Text, true, cts);

            while (client.State == WebSocketState.Open)
            {
                var buffer = WebSocket.CreateClientBuffer(1024 * 64, 1024);
                WebSocketReceiveResult result = null;
                using (var ms = new MemoryStream())
                {
                    result = await client.ReceiveAsync(ms, buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var reader = new StreamReader(ms, Encoding.UTF8);
                        var payload = await reader.ReadToEndAsync();
                        var rootelement = JsonDocument.Parse(payload).RootElement;
                        if(rootelement.GetProperty("event").GetString() == "trade")
                        {
                            BTCUSDPrice = rootelement.GetProperty("data").GetProperty("price").GetDecimal();
                        } else
                        {
                            continue;
                        }
                    }
                }
            }
        }

    }
}
