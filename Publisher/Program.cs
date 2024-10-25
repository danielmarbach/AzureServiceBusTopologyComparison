using Microsoft.Extensions.Azure;
using Publisher.Services;
using Publisher.Models;

var builder = Host.CreateApplicationBuilder(args);

// Configure options
builder.Services.Configure<PublisherOptions>(
    builder.Configuration.GetSection(PublisherOptions.ConfigurationSection));

builder.Services.AddAzureClients(azureClientBuilder =>
{
    azureClientBuilder.AddServiceBusClient(builder.Configuration.GetSection("ServiceBus")["ConnectionString"]);
    azureClientBuilder.AddServiceBusAdministrationClient(builder.Configuration.GetSection("ServiceBus")["ConnectionString"]);
});

// Register services in correct order
builder.Services.AddHostedService<ServiceBusInitializationService>();
builder.Services.AddHostedService<PublisherService>();

var host = builder.Build();
await host.RunAsync();
