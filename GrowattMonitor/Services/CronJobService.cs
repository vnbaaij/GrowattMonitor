using System;
using System.Threading;
using System.Threading.Tasks;
using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GrowattMonitor.Services;

public abstract class CronJobService : IHostedService, IDisposable
{
    private System.Timers.Timer _timer;
    private readonly CronExpression _expression;
    private readonly TimeZoneInfo _timeZoneInfo;
    
    protected CronJobService(string cronExpression, TimeZoneInfo timeZoneInfo)
    {
        _expression = CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds);
        _timeZoneInfo = timeZoneInfo;
    }

    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        await ScheduleJob(cancellationToken);
    }

    public virtual async Task ScheduleJob(CancellationToken cancellationToken)
    {
        var next = _expression.GetNextOccurrence(DateTimeOffset.Now, _timeZoneInfo);
        if (!next.HasValue)
        {
            return;
        }
        
        TimeSpan delay = next.Value - DateTimeOffset.Now;
        if (delay.TotalMilliseconds <= 0)   // prevent non-positive values from being passed into Timer
        {
            await ScheduleJob(cancellationToken);
        }
        
        _timer = new System.Timers.Timer(delay.TotalMilliseconds);
        _timer.Elapsed += async (sender, args) =>
        {
            _timer.Dispose();  // reset and dispose timer
            _timer = null;

            if (!cancellationToken.IsCancellationRequested)
            {
                await DoWork(cancellationToken);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await ScheduleJob(cancellationToken);    // reschedule next
            }
        };
        
        _timer.Start();
    }

    public virtual async Task DoWork(CancellationToken cancellationToken)
    {
        await Task.Delay(5000, cancellationToken);  // do the work
    }

    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Stop();
        return Task.CompletedTask;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "<Pending>")]
    public virtual void Dispose()
    {
        _timer?.Dispose();
    }
}
