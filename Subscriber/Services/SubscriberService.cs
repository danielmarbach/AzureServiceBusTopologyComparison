using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Subscriber.Models;

namespace Subscriber.Services;

public class SubscriberService : IHostedService
{
    private readonly ServiceBusProcessor _processor;
    private readonly ILogger<SubscriberService> _logger;

    public SubscriberService(ServiceBusClient serviceBusClient, IOptions<SubscriberOptions> options,
        ILogger<SubscriberService> logger)
    {
        _logger = logger;

        var processorOptions = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = options.Value.ThroughputPerMinute,
            PrefetchCount = options.Value.ThroughputPerMinute,
            AutoCompleteMessages = true // Enable autocompletion
        };

        _processor = serviceBusClient.CreateProcessor(options.Value.QueueName, processorOptions);

        _processor.ProcessMessageAsync += ProcessMessageHandler;
        _processor.ProcessErrorAsync += ErrorHandler;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting the Service Bus processor.");
        await _processor.StartProcessingAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping the Service Bus processor.");
        await _processor.StopProcessingAsync(cancellationToken);
        await _processor.DisposeAsync();
    }

    private Task ProcessMessageHandler(ProcessMessageEventArgs args)
    {
        try
        {
            var messageBody = args.Message.Body.ToString();
            _logger.LogInformation("Received message: {MessageBody}", messageBody);

            if (args.Message.ApplicationProperties.Count > 0)
            {
                _logger.LogInformation("Application Properties:");
                foreach (var property in args.Message.ApplicationProperties)
                {
                    _logger.LogInformation("  {Key}: {Value}", property.Key, property.Value);
                }
            }
            else
            {
                _logger.LogInformation("No application properties found.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message.");
        }

        return Task.CompletedTask;
    }

    private Task ErrorHandler(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Error in the Service Bus processor.");
        return Task.CompletedTask;
    }
}