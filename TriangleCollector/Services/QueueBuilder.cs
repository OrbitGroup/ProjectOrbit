using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Collections.Concurrent;
using TriangleCollector.Models;
using System.Linq;

namespace TriangleCollector.Services
{
    public class QueueBuilder : BackgroundService
    {
        private int buffer = 1; //time (in seconds) of the interval that the queue builder evaluates orderbook updates

        public static List<string> triangleIDs = new List<string>();
        public static ConcurrentDictionary<string,Triangle> uniqueTriangles = new ConcurrentDictionary<string, Triangle>();
        
        private readonly ILoggerFactory _factory;

        private readonly ILogger<QueueMonitor> _logger;


        public QueueBuilder(ILoggerFactory factory, ILogger<QueueMonitor> logger)
        {
            _factory = factory;
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting Queue Builder...");

            stoppingToken.Register(() => _logger.LogDebug("Stopping Queue Builder..."));
            await BackgroundProcessing(stoppingToken);
            _logger.LogDebug("Stopped Queue Builder.");
        }
        private async Task BackgroundProcessing(CancellationToken stoppingtoken)
        {
            while (!stoppingtoken.IsCancellationRequested)
            {
                Thread.Sleep(buffer * 1000);
                var redundantSymbols = triangleIDs.Count - uniqueTriangles.Count;
                _logger.LogDebug($"{redundantSymbols} redundant triangles in the last {buffer} seconds.");

                Monitor.Enter(uniqueTriangles);
                Monitor.Enter(triangleIDs);
                try
                {
                    foreach (var UniqueTriangle in uniqueTriangles) 
                    {
                        TriangleCollector.TrianglesToRecalculate.Enqueue(UniqueTriangle.Value);
                    }
                    triangleIDs.Clear();
                    uniqueTriangles.Clear(); 

                } finally
                {
                    Monitor.Exit(uniqueTriangles);
                    Monitor.Exit(triangleIDs);
                }
            }
        }
    }
}
