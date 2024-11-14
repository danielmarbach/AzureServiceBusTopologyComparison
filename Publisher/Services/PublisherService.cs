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
            // The send destination overrides the bundle topic name.
            // This mode is primarily used to get a baseline between publishes and direct sends
            if (!string.IsNullOrWhiteSpace(_options.SendDestination))
            {
                senders[i % range.Length] = serviceBusClient.CreateSender(_options.SendDestination);
            }
            else if (_options.TopologyType is "MassTransit" or "SNS")
            {
                var messageType = string.Format(_options.MessageTypesTemplate, i);
                var destination = messageType.Split(';', StringSplitOptions.RemoveEmptyEntries).First().Trim();
                senders[i % range.Length] = serviceBusClient.CreateSender(destination);
            }
            else
            {
                senders[i % range.Length] = serviceBusClient.CreateSender(_options.BundleTopicName);
            }
        }

        var tokenLimitPerInterval = _options.ThroughputPerMinute + range.Length + 1;
        var rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = tokenLimitPerInterval,
            QueueLimit = tokenLimitPerInterval,              // No queued requests
            ReplenishmentPeriod = TimeSpan.FromMinutes(1), // ASB measure interval is 1 min
            TokensPerPeriod = tokenLimitPerInterval
        });

        var messageId = 0;
        var fromInclusive = range.First();
        var exclusive = range.Last();
        while (!stoppingToken.IsCancellationRequested)
        {
            using var _ = await rateLimiter.AcquireAsync(1, stoppingToken);
            
            await Parallel.ForAsync(0, range.Length, stoppingToken, async (_, outerToken) =>
            {
                using var __ = await rateLimiter.AcquireAsync(1, outerToken);

                await Parallel.ForAsync(fromInclusive, exclusive, outerToken, async (i, innerToken) =>
                {
                    using var ___ = await rateLimiter.AcquireAsync(1, innerToken);

                    var messageType = string.Format(_options.MessageTypesTemplate, i);
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
                    if (_options.TopologyType is "CorrelationFilter" or "CorrelationFilterWithoutInheritance")
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