using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GrowattMonitorShared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GrowattMonitor
{
    public class GrowattWorker : BackgroundService
    {
        private readonly ILogger<GrowattWorker> _logger;
        private readonly InverterMonitor _monitor;

        public GrowattWorker(ILogger<GrowattWorker> logger, InverterMonitor monitor)
        {
            _logger = logger;
            _monitor = monitor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                Console.OutputEncoding = Encoding.Default;

                _monitor.Run();

                //byte[] bytes = new byte[] { 0x00, 0xA8, 0x00, 0x05, 0x00, 0x0C, 0x01, 0x16, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75, 0x45, 0x2E, 0x79, 0xC6 };
                ////data = ByteArrayToString(bytes);

                //ProcessBytes(bytes);
                await Task.Delay(1000, stoppingToken);
            }
        }

    }
}
