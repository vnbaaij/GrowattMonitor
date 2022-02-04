using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Azure.Data.Tables;
using GrowattMonitor.Configuration;
using GrowattMonitor.Helpers;
using Microsoft.Extensions.Options;

namespace GrowattMonitor;

public class InverterMonitorMock
{
    private readonly ILogger<InverterMonitor> _logger;
    private readonly AppConfig _config;
    

    //private TableClient _table;

    private readonly CancellationTokenSource _cancellation;

    public InverterMonitorMock(ILogger<InverterMonitor> logger, IOptions<AppConfig> config)
    {
        _logger = logger;
        _config = config.Value;

        _logger.LogDebug("StorrageConnectionString: {StorageConnectionstring}", _config.StorageConnectionstring);

        _cancellation = new();
    }

    public async Task Run()
    {
        _logger.LogInformation("Mock running...");

        // Enter the listening loop.
        while (IsDayLight() && !_cancellation.IsCancellationRequested)
        {
            try
            {
                //Simulate doing something
                await Task.Delay(3000);
                _logger.LogInformation("Mock done...");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Mock canceled!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in Mock while running: ");
                _cancellation.Cancel();
            }
        }


        if (!IsDayLight())
        {
            _logger.LogInformation("Outside of daylight period...");
        }
        _logger.LogInformation("Mock stopped...");

        //_cancellation.Cancel();
        


    }

    private bool IsDayLight()
    {
        return Utils.IsDaylight(_config.Latitude, _config.Longitude, _config.Offset);
    }
}
