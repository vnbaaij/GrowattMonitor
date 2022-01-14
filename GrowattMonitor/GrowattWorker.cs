using System.Text;
using GrowattMonitor.Configuration;
using Microsoft.Extensions.Options;

namespace GrowattMonitor;

public class GrowattWorker : BackgroundService
{
    private readonly ILogger<GrowattWorker> _logger;
    private readonly InverterMonitor _monitor;

    public readonly AppConfig _config;

    public DateTime _riseTime = DateTime.MinValue, _setTime = DateTime.MinValue;

    public GrowattWorker(ILogger<GrowattWorker> logger, InverterMonitor monitor, IOptions<AppConfig> config)
    {
        _logger = logger;
        _monitor = monitor;
        _config = config.Value;
    }
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"GrowattMonitor started at: {DateTimeOffset.Now}");

        await base.StartAsync(cancellationToken);
    }
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker executing at: {time}", DateTimeOffset.Now);

            Console.OutputEncoding = Encoding.Default;

            // Only run the monitor when there is actual daylight
            if (Utils.IsDaylight(_config.Latitude, _config.Longitude))
                await _monitor.Run();

            // Wait for 1 minute
            Console.WriteLine("Sleeping for 10 minutes...");
            await Task.Delay(600000, cancellationToken);
            
        }
    }
}
