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
        await using var sender = serviceBusClient.CreateSender(_options.TopologyType == "MassTransit" ? _options.MessageTypeTemplate.Split(';', StringSplitOptions.RemoveEmptyEntries).First() : BundleTopicName);

        var messageId = 0;
        var messages = new List<ServiceBusMessage>(_options.NumberOfMessages);

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

                messages.Add(message);
                logger.LogInformation("Prepared message {MessageId} with MessageType {MessageType}",
                    messageId, messageType);
            }

            await sender.SendMessagesAsync(messages, stoppingToken);
            logger.LogInformation("Sent {MessageCount} messages in a single call", messages.Count);

            messages.Clear();

            var jitter = Random.Shared.Next(50);
            await Task.Delay(200 + jitter, stoppingToken);
        }
    }
}