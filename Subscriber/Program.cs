using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Subscriber.Services;
using Subscriber.Models;

var builder = Host.CreateApplicationBuilder(args);

// Configure options
builder.Services.Configure<SubscriberOptions>(
    builder.Configuration.GetSection(SubscriberOptions.ConfigurationSection));

// Add services to the container
builder.Services.AddSingleton(sp => 
    new ServiceBusAdministrationClient(
        builder.Configuration["ServiceBus:Namespace"]!, 
        new DefaultAzureCredential()));

builder.Services.AddSingleton(sp => 
    new ServiceBusClient(
        builder.Configuration["ServiceBus:Namespace"]!, 
        new DefaultAzureCredential()));

// Register services in correct order
builder.Services.AddHostedService<ServiceBusInitializationService>();

var host = builder.Build();
await host.RunAsync();
