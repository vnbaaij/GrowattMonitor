using Cronos;
using GrowattMonitor.Configuration;
using GrowattMonitor.Helpers;
using GrowattMonitor.Models;
using Microsoft.Extensions.Options;

namespace GrowattMonitor.Services;

public class GrowattWorkerJob : CronJobService
{
    private readonly ILogger<GrowattWorkerJob> _logger;
    private readonly InverterMonitor _monitor;

    public GrowattWorkerJob(IScheduleConfig<GrowattWorkerJob> schedule, ILogger<GrowattWorkerJob> logger, InverterMonitor monitor)
        : base (schedule.CronExpression, schedule.TimeZoneInfo)
    {
        _logger = logger;
        _monitor = monitor;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Growatt Worker Job starting...");
        return base.StartAsync(cancellationToken);
    }


    public override Task DoWork(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Growatt Worker Job calling monitor at: {time:hh:mm:ss}", DateTime.Now);
        
        _monitor.Run().Wait(cancellationToken);
        
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Growatt Worker Job stopping...");
        return base.StopAsync(cancellationToken);
    }
}
