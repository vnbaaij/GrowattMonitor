using GrowattMonitor.Models;

namespace GrowattMonitor.Services;

public class TestJob : CronJobService
{
    private readonly ILogger<TestJob> logger;
    private readonly InverterMonitorMock monitor;

    public TestJob(IScheduleConfig<TestJob> schedule, ILogger<TestJob> logger, InverterMonitorMock monitor)
        : base(schedule.CronExpression, schedule.TimeZoneInfo)
    {
        this.logger = logger;
        this.monitor = monitor;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("TestJob starting...");
        return base.StartAsync(cancellationToken);
    }

    public override Task DoWork(CancellationToken cancellationToken)
    {
        logger.LogInformation("{time:hh:mm:ss} TestJob work starting.",DateTime.Now);
        monitor.Run().Wait(cancellationToken);
        logger.LogInformation("{time:hh:mm:ss} TestJob work finished.", DateTime.Now);

        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("TestJob stopping...");
        return base.StopAsync(cancellationToken);
    }
}
