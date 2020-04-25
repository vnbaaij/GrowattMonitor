using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Cosmos;
using GrowattMonitor.Configuration;
using GrowattMonitorShared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GrowattMonitor
{
    public class GrowattWorker : BackgroundService
    {
        private readonly ILogger<GrowattWorker> _logger;
        private readonly InverterMonitor _monitor;

        //private readonly HistoryRewriter _history;
        public readonly AppConfig _config;
        private CosmosClient _cosmosClient;
        private CosmosContainer _cosmosContainer;

        public DateTime _riseTime=DateTime.MinValue, _setTime =DateTime.MinValue;

        //public GrowattWorker(ILogger<GrowattWorker> logger, InverterMonitor monitor, HistoryRewriter history, IOptions<AppConfig> config)
        //{
        //    _logger = logger;
        //    _monitor = monitor;
        //    _history = history;
        //    _config = config.Value;
        //}

        public GrowattWorker(ILogger<GrowattWorker> logger, InverterMonitor monitor, IOptions<AppConfig> config)
        {
            _logger = logger;
            _monitor = monitor;
            _config = config.Value;
        }
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"GrowattMontor started at: {DateTimeOffset.Now}");
            
            var endpointUrl = _config.CosmosDBSettings.EndpointUrl;
            var authorizationKey = _config.CosmosDBSettings.AuthorizationKey;
            var database = _config.CosmosDBSettings.Database;
            var container = _config.CosmosDBSettings.Container;

            //Init CosmosDB here
            
            //ConnectionPolicy connectionPolicy = new ConnectionPolicy();
            //connectionPolicy.RetryOptions.MaxRetryAttemptsOnThrottledRequests = 3;
            //connectionPolicy.RetryOptions.MaxRetryWaitTimeInSeconds = 60;

            var options = new CosmosClientOptions()
            {
                Serializer = new CosmosJsonSerializer(),
            };
            _cosmosClient = new CosmosClient(endpointUrl, authorizationKey, options);
            _ = await _cosmosClient.CreateDatabaseIfNotExistsAsync(database);
            _ = await _cosmosClient.GetDatabase(database).CreateContainerIfNotExistsAsync(container, "/key");

            _cosmosContainer = _cosmosClient.GetContainer(_config.CosmosDBSettings.Database, _config.CosmosDBSettings.Container);

            await base.StartAsync(cancellationToken);
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker executing at: {time}", DateTimeOffset.Now);

                Console.OutputEncoding = Encoding.Default;

                //if (_config.Ids != null)
                //{
                //    _history.Run(_cosmosContainer);
                //}

                //if (Utils.IsDaylight(_config.Latitude, _config.Longitude))
                    await _monitor.Run(_cosmosContainer);

                // Wait for 5 minutes
                Console.WriteLine("Sleeping for 5 minutes...");
                await Task.Delay(300000, stoppingToken);
            }
        }

    }
}
