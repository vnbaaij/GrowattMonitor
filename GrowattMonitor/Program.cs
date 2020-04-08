using GrowattMonitor.Configuration;
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
                    services.AddOptions();

                    services.Configure<AppConfig>(hostContext.Configuration.GetSection("Configuration"));
                    services.AddSingleton<InverterMonitor>();
                    services.AddSingleton<HistoryRewriter>();
                    services.AddHostedService<GrowattWorker>();
                });
    }
}
