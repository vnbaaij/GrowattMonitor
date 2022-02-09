using System.Text;
using GrowattMonitor.Configuration;
using GrowattMonitor.Helpers;
using Microsoft.Extensions.Options;

namespace GrowattMonitor;

public class GrowattWorker : BackgroundService
{
    private readonly ILogger<GrowattWorker> logger;
    private readonly InverterMonitor monitor;
    private readonly Utils utils;
    public readonly AppConfig config;

    public DateTime riseTime = DateTime.MinValue, setTime = DateTime.MinValue;

    public GrowattWorker(ILogger<GrowattWorker> logger, InverterMonitor monitor, IOptions<AppConfig> config, Utils utils)
    {
        this.logger = logger;
        this.monitor = monitor;
        this.utils = utils;
        this.config = config.Value;
    }
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("GrowattMonitor started at: {time}", DateTimeOffset.Now);

        await base.StartAsync(cancellationToken);
    }
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Worker executing at: {time}", DateTimeOffset.Now);

            Console.OutputEncoding = Encoding.Default;

            // Only run the monitor when there is actual daylight
            if (utils.IsDaylight())
                await monitor.Run();

            // Wait for 1 minute
            Console.WriteLine("Sleeping for 10 minutes...");
            await Task.Delay(600000, cancellationToken);

        }
    }
}
