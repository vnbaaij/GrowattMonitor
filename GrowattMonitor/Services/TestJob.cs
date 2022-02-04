﻿using GrowattMonitor.Models;

namespace GrowattMonitor.Services;

public class TestJob : CronJobService
{
    private readonly ILogger<TestJob> _logger;
    private readonly InverterMonitorMock _monitor;

    public TestJob(IScheduleConfig<TestJob> schedule, ILogger<TestJob> logger, InverterMonitorMock monitor)
        : base(schedule.CronExpression, schedule.TimeZoneInfo)
    {
        _logger = logger;
        _monitor = monitor;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TestJob starting...");
        return base.StartAsync(cancellationToken);
    }

    public override Task DoWork(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{time:hh:mm:ss} TestJob work starting.",DateTime.Now);
        _monitor.Run().Wait(cancellationToken);
        _logger.LogInformation("{time:hh:mm:ss} TestJob work finished.", DateTime.Now);

        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TestJob stopping...");
        return base.StopAsync(cancellationToken);
    }
}
