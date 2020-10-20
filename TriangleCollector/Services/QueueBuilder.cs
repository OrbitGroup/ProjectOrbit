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
using System.Runtime.InteropServices.ComTypes;

namespace TriangleCollector.Services
{
    public class QueueBuilder : BackgroundService
    {
        private int buffer = 1; //time (in seconds) of the interval that the queue builder evaluates orderbook updates

        public static ConcurrentQueue<Triangle> updateQueue = new ConcurrentQueue<Triangle>();

        private ConcurrentDictionary<string, DateTime> QueueTimes = new ConcurrentDictionary<string, DateTime>(); //stores the most recent queue time for all triangle IDs
        
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
                bool triangleDequeued = updateQueue.TryDequeue(out Triangle impactedTriangle);
                if(triangleDequeued)
                {
                    bool previouslyQueued = QueueTimes.TryGetValue(impactedTriangle.TriangleID, out DateTime lastTime);
                    if (previouslyQueued)
                    {
                        var queueDelay = DateTime.UtcNow - lastTime;
                        if (queueDelay.TotalSeconds > buffer) //queue triangle update if this distinct triangle hasn't been updated in N seconds
                        {
                            TriangleCollector.TrianglesToRecalculate.Enqueue(impactedTriangle);
                        }
                    }
                    else //this disctinct triangle hasn't been queued yet this session
                    {
                        TriangleCollector.TrianglesToRecalculate.Enqueue(impactedTriangle);
                        QueueTimes.TryAdd(impactedTriangle.TriangleID, DateTime.UtcNow);
                    }
                }
            }
        }
    }
}
