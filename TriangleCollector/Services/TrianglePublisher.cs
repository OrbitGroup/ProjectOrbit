using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using TriangleCollector.Models;

namespace TriangleCollector.Services
{
    public class TrianglePublisher : BackgroundService
    {
        private readonly ILogger<TrianglePublisher> _logger;

        private ConnectionMultiplexer redis;

        private IDatabase db;

        private ISubscriber subscriber;

        public TrianglePublisher(ILogger<TrianglePublisher> logger)
        {
            _logger = logger;
            redis = ConnectionMultiplexer.Connect(System.IO.File.ReadAllText("/mnt/secrets-store/redis"));
            db = redis.GetDatabase();
            subscriber = redis.GetSubscriber();
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Starting triangle publisher...");
            await Task.Run(async () => await PublishTriangles(stoppingToken));
        }

        private async Task PublishTriangles(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                /*try
                {
                    if (TriangleCollector.RecalculatedTriangles.TryDequeue(out Triangle triangle))
                    {
                        await db.HashSetAsync(triangle.ToString(), new HashEntry[] { new HashEntry("profit", triangle.ProfitPercent.ToString()) });
                        subscriber.PublishAsync("triangles", triangle.ToString());
                    }
                    
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex.Message);
                }*/
            }
        }
    }
}
