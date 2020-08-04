using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using TriangleCollector.Models;

namespace TriangleCollector.Services
{
    public class SymbolMonitor : BackgroundService
    {
        private readonly ILogger<SymbolMonitor> _logger;

        public SymbolMonitor(ILogger<SymbolMonitor> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting Symbol Monitor...");
            stoppingToken.Register(() => _logger.LogDebug("Stopping Symbol Monitor..."));
            await Task.Run(async () =>
            {
                await BackgroundProcessing(stoppingToken);
            }, stoppingToken);
        }


        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            //get updatedSymbols
            //get impacted triangles
            //push triangles to trianglesToRecalculate
            while (TriangleCollector.client.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
            {
                if (TriangleCollector.UpdatedSymbols.TryDequeue(out string symbol) && TriangleCollector.SymbolTriangleMapping.TryGetValue(symbol, out List<Triangle> impactedTriangles))
                {
                    impactedTriangles.ForEach(TriangleCollector.TrianglesToRecalculate.Enqueue);
                }
            }
        }
    }
}
