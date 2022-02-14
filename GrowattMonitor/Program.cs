using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using GrowattMonitor;
using GrowattMonitor.Configuration;
using GrowattMonitor.Helpers;
using GrowattMonitor.Models;
using GrowattMonitor.Services;

using System.Security.Cryptography.X509Certificates;

IHost host = Host.CreateDefaultBuilder(args)
    .UseSystemd()
    .ConfigureAppConfiguration((hostContext, config) =>
    {
        IConfigurationRoot builtConfig = config.Build();

        using X509Store store = new(StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        var x509Certificate = store.Certificates
        .Find(
            X509FindType.FindByThumbprint,
            builtConfig["AzureADCertThumbprint"],
            validOnly: false)
        .OfType<X509Certificate2>()
        .Single();

        X509Certificate2Collection certs = store.Certificates
        .Find(X509FindType.FindByThumbprint,
            builtConfig["AzureADCertThumbprint"], false);

        config.AddAzureKeyVault(new Uri($"https://{builtConfig["KEYVAULTNAME"]}.vault.azure.net/"),
                                new ClientCertificateCredential(
                                    builtConfig["AZUREADDIRECTORYID"], 
                                    builtConfig["AZUREADAPPLICATIONID"], 
                                    x509Certificate)
                                );


        store.Close();
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddOptions();

        services.Configure<AppConfig>(hostContext.Configuration.GetSection("Configuration"));
        services.AddSingleton<Utils>();
        services.AddSingleton<StorageHelper>();
        services.AddSingleton<InverterMonitor>();
        //services.AddSingleton<InverterMonitorMock>();

        //services.AddCronJob<TestJob>(c =>
        //{
        //     c.TimeZoneInfo = TimeZoneInfo.Local;
        //     c.CronExpression = @"*/15 * 7-17 * * *";
        //});

        services.AddCronJob<GrowattWorkerJob>(c =>
        {
            c.TimeZoneInfo = TimeZoneInfo.Local;
            c.CronExpression = @"0 */1 7-21 * * *";
        });

    })
    .Build();

await host.RunAsync();
