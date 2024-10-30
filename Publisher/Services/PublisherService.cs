using System.Threading.RateLimiting;
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var range = _options.EventRange.ToRange();
        var senders = new ServiceBusSender[range.Length];
        foreach (var i in range)
        {
            if (_options.TopologyType == "MassTransit")
            {
                var messageType = string.Format(_options.MessageTypeTemplate, i);
                var destination = messageType.Split(';', StringSplitOptions.RemoveEmptyEntries).First().Trim();
                senders[i % range.Length] = serviceBusClient.CreateSender(destination);
            }
            else
            {
                senders[i % range.Length] = serviceBusClient.CreateSender(_options.BundleTopicName);
            }
        }
        
        var rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = ((int)(range.Length * _options.PublishMultiplier)) + range.Length,
            QueueLimit = 0,              // No queued requests
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            TokensPerPeriod = (int)(range.Length * _options.PublishMultiplier)
        });

        var messageId = 0;
        var fromInclusive = range.First();
        var exclusive = range.Last();
        while (!stoppingToken.IsCancellationRequested)
        {
            await Parallel.ForAsync(0, range.Length, stoppingToken, async (_, outerToken) =>
            {
                await Parallel.ForAsync(fromInclusive, exclusive, outerToken, async (i, innerToken) =>
                {
                    // Request permit before creating and sending a message
                    using var lease = await rateLimiter.AcquireAsync(1, innerToken);
                    if (!lease.IsAcquired)
                    {
                        return;
                    }

                    var messageType = string.Format(_options.MessageTypeTemplate, i);
                    var incrementedMessageId = Interlocked.Increment(ref messageId);
                    var message = new ServiceBusMessage($"Message {incrementedMessageId} at {DateTime.UtcNow:O}")
                    {
                        MessageId = incrementedMessageId.ToString(),
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
                        incrementedMessageId, messageType);

                    await senders[i % range.Length].SendMessageAsync(message, innerToken);
                    logger.LogInformation("Sent message with {MessageId} to destination {Destination}",
                        message.MessageId, senders[i % range.Length].EntityPath);
                });
            });
        }

        foreach (var sender in senders)
        {
            await sender.DisposeAsync();
        }
    }
}