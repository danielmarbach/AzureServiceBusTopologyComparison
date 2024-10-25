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
    private readonly string[] _messageTypes = options.Value.MessageTypes;
    private readonly string _topologyType = options.Value.TopologyType; // Inject topology type
    private const string TopicName = "bundle-1";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var sender = serviceBusClient.CreateSender(TopicName);
        var messageId = 0;
        var messages = new List<ServiceBusMessage>(_messageTypes.Length);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var messageType in _messageTypes)
            {
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
                if (_topologyType == "CorrelationFilter")
                {
                    var splitValues = messageType.Split([';'], StringSplitOptions.RemoveEmptyEntries);
                    foreach (var value in splitValues)
                    {
                        message.ApplicationProperties[value.Trim()] = true;
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