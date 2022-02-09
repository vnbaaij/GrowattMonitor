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
    private readonly ILogger<InverterMonitor> logger;
    private readonly Utils utils;
    private readonly AppConfig config;
    

    //private TableClient _table;

    private readonly CancellationTokenSource _cancellation;

    public InverterMonitorMock(ILogger<InverterMonitor> logger, IOptions<AppConfig> config, Utils utils)
    {
        this.logger = logger;
        this.utils = utils;
        this.config = config.Value;

        this.logger.LogDebug("StorrageConnectionString: {StorageConnectionstring}", this.config.StorageConnectionstring);

        _cancellation = new();
    }

    public async Task Run()
    {
        logger.LogInformation("Mock running...");

        // Enter the listening loop.
        while (utils.IsDaylight() && !_cancellation.IsCancellationRequested)
        {
            try
            {
                //Simulate doing something
                await Task.Delay(3000);
                logger.LogInformation("Mock done...");
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Mock canceled!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception in Mock while running: ");
                _cancellation.Cancel();
            }
        }


        if (!utils.IsDaylight())
        {
            logger.LogInformation("Outside of daylight period...");
        }
        logger.LogInformation("Mock stopped...");

        //_cancellation.Cancel();
        


    }

    
}
