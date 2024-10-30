using Microsoft.Extensions.Azure;
using TopologyCleaner.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAzureClients(azureClientBuilder =>
{
    azureClientBuilder.AddServiceBusAdministrationClient(builder.Configuration.GetSection("ServiceBus")["ConnectionString"]);
});

builder.Services.AddHostedService<ServiceBusInitializationService>();

var host = builder.Build();
await host.RunAsync();
