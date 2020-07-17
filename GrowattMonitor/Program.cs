using System.Linq;
using System.Security.Cryptography.X509Certificates;
using GrowattMonitor.Configuration;
using Microsoft.Extensions.Configuration;
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
                .UseSystemd()
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    if (hostContext.HostingEnvironment.IsProduction())
                    {
                        var builtConfig = config.Build();

                        using var store = new X509Store(StoreLocation.CurrentUser);
                        store.Open(OpenFlags.ReadOnly);
                        var certs = store.Certificates
                            .Find(X509FindType.FindByThumbprint,
                                builtConfig["AZUREADCERTTHUMBPRINT"], false);

                        config.AddAzureKeyVault(
                            $"https://{builtConfig["KEYVAULTNAME"]}.vault.azure.net/",
                            builtConfig["AZUREADAPPLICATIONID"],
                            certs.OfType<X509Certificate2>().Single());

                        store.Close();
                    }
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddOptions();

                    services.Configure<AppConfig>(hostContext.Configuration.GetSection("Configuration"));
                    services.AddSingleton<InverterMonitor>();
                    services.AddHostedService<GrowattWorker>();
                });
    }
}
