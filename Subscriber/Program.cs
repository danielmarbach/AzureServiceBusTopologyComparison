using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Subscriber.Services;
using Subscriber.Models;

var builder = Host.CreateApplicationBuilder(args);

// Configure options
builder.Services.Configure<SubscriberOptions>(
    builder.Configuration.GetSection(SubscriberOptions.ConfigurationSection));

builder.Services.AddAzureClients(azureClientBuilder =>
{
    azureClientBuilder.AddServiceBusClient(builder.Configuration.GetSection("ServiceBus")["ConnectionString"]);
    azureClientBuilder.AddServiceBusAdministrationClient(builder.Configuration.GetSection("ServiceBus")["ConnectionString"]);
});

// Register services in correct order
builder.Services.AddHostedService<ServiceBusInitializationService>();
builder.Services.AddHostedService<SubscriberService>();

var host = builder.Build();
await host.RunAsync();
