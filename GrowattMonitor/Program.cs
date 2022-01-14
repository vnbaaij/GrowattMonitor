using System.Security.Cryptography.X509Certificates;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using GrowattMonitor;
using GrowattMonitor.Configuration;

IHost host = Host.CreateDefaultBuilder(args)
    .UseSystemd()
    .ConfigureAppConfiguration((hostContext, config) =>
    {
        //if (hostContext.HostingEnvironment.IsProduction())
        //{
        IConfigurationRoot builtConfig = config.Build();

        using X509Store store = new(StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        X509Certificate2Collection certs = store.Certificates
        .Find(X509FindType.FindByThumbprint,
            builtConfig["AzureADCertThumbprint"], false);

        config.AddAzureKeyVault(new Uri($"https://{builtConfig["KEYVAULTNAME"]}.vault.azure.net/"),
                                new ClientCertificateCredential(builtConfig["AZUREADDIRECTORYID"], builtConfig["AZUREADAPPLICATIONID"], certs.OfType<X509Certificate2>().Single()),
                                new KeyVaultSecretManager());


        store.Close();

        //config.AddAzureKeyVault(new System.Uri($"https://{builtConfig["KEYVAULTNAME"]}.vault.azure.net/"),
        //    builtConfig["AZUREADAPPLICATIONID"],
        //     certs.OfType<X509Certificate2>().Single());

        //}
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddOptions();

        services.Configure<AppConfig>(hostContext.Configuration.GetSection("Configuration"));
        services.AddSingleton<InverterMonitor>();
        services.AddHostedService<GrowattWorker>();
    })
    .Build();

await host.RunAsync();
