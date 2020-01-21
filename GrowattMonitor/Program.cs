using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrowattMonitorShared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GrowattMonitor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<InverterMonitor>();
                    services.AddHostedService<GrowattWorker>();
                });
    }
}
