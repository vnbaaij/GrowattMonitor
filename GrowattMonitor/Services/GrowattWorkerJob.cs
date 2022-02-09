using Cronos;
using GrowattMonitor.Configuration;
using GrowattMonitor.Helpers;
using GrowattMonitor.Models;
using Microsoft.Extensions.Options;

namespace GrowattMonitor.Services;

public class GrowattWorkerJob : CronJobService
{
    private readonly ILogger<GrowattWorkerJob> logger;
    private readonly InverterMonitor monitor;

    public GrowattWorkerJob(IScheduleConfig<GrowattWorkerJob> schedule, ILogger<GrowattWorkerJob> logger, InverterMonitor monitor)
        : base (schedule.CronExpression, schedule.TimeZoneInfo)
    {
        this.logger = logger;
        this.monitor = monitor;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Growatt Worker Job starting...");
        return base.StartAsync(cancellationToken);
    }


    public override Task DoWork(CancellationToken cancellationToken)
    {
        logger.LogInformation("Growatt Worker Job calling monitor at: {time:hh:mm:ss}", DateTime.Now);
        
        monitor.Run().Wait(cancellationToken);
        
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Growatt Worker Job stopping...");
        return base.StopAsync(cancellationToken);
    }
}
