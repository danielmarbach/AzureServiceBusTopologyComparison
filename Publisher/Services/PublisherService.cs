using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Publisher.Models;

namespace Publisher.Services;

public class PublisherService(
    ServiceBusClient serviceBusClient,
    IOptions<PublisherOptions> options,
    ILogger<PublisherService> logger)
    : BackgroundService
{
    private readonly PublisherOptions _options = options.Value;
    private const string BundleTopicName = "bundle-1";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var senders = new ServiceBusSender[_options.NumberOfMessages];
        for (var i = 0; i < _options.NumberOfMessages; i++)
        {
            if (_options.TopologyType == "MassTransit")
            {
                var messageType = string.Format(_options.MessageTypeTemplate, i);
                var destination = messageType.Split(';', StringSplitOptions.RemoveEmptyEntries).First().Trim();
                senders[i] = serviceBusClient.CreateSender(destination);
            }
            else
            {
                senders[i] = serviceBusClient.CreateSender(BundleTopicName);
            }
        }

        var messageId = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            for (var i = 0; i < _options.NumberOfMessages; i++)
            {
                var messageType = string.Format(_options.MessageTypeTemplate, i);
                var message = new ServiceBusMessage($"Message {++messageId} at {DateTime.UtcNow:O}")
                {
                    MessageId = messageId.ToString(),
                    Subject = "test-message",
                    ApplicationProperties =
                    {
                        { "MessageType", messageType }
                    }
                };

                // Check topology type and add additional application properties if needed
                if (_options.TopologyType == "CorrelationFilter")
                {
                    var hierarchyTypes = messageType.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var hierarchyType in hierarchyTypes)
                    {
                        message.ApplicationProperties[hierarchyType.Trim()] = true;
                    }
                }

                logger.LogInformation("Prepared message {MessageId} with MessageType {MessageType}",
                    messageId, messageType);
                
                await senders[i].SendMessageAsync(message, stoppingToken);
                logger.LogInformation("Sent message with {MessageId} to destination {Destination}", message.MessageId, senders[i].EntityPath);
            }

            var jitter = Random.Shared.Next(50);
            await Task.Delay(200 + jitter, stoppingToken);
        }

        foreach (var sender in senders)
        {
            await sender.DisposeAsync();
        }
    }
}