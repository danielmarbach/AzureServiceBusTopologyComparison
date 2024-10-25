using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Subscriber.Models;

namespace Subscriber.Services;

public class ServiceBusInitializationService(
    ServiceBusAdministrationClient adminClient,
    IOptions<SubscriberOptions> options,
    ILogger<ServiceBusInitializationService> logger)
    : IHostedService
{
    private readonly string _queueName = options.Value.QueueName;
    private readonly string _topologyType = options.Value.TopologyType;
    private readonly string[] _messageTypeFilters = options.Value.MessageTypeFilters;
    private const string TopicName = "bundle-1";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await CreateInputQueue(cancellationToken);

        logger.LogInformation("Creating topology: {TopologyType}", _topologyType);
        switch (_topologyType)
        {
            case "SqlFilter":
                await InitializeSqlFilterTopology(cancellationToken);
                break;
            case "CorrelationFilter":
                await InitializeCorrelationFilterTopology(cancellationToken);
                break;
        }
    }

    private async Task CreateInputQueue(CancellationToken cancellationToken)
    {
        // Queue initialization
        try
        {
            if (await adminClient.QueueExistsAsync(_queueName, cancellationToken))
            {
                logger.LogInformation("Deleting existing queue: {QueueName}", _queueName);
                await adminClient.DeleteQueueAsync(_queueName, cancellationToken);
            }

            logger.LogInformation("Creating queue: {QueueName}", _queueName);
            await adminClient.CreateQueueAsync(_queueName, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing queue: {QueueName}", _queueName);
            throw;
        }
    }

    private async Task InitializeCorrelationFilterTopology(CancellationToken cancellationToken)
    {
        await CreateBundleTopicIfNecessary(cancellationToken);
    }

    private async Task InitializeSqlFilterTopology(CancellationToken cancellationToken)
    {
        // Topic initialization
        try
        {
            await CreateBundleTopicIfNecessary(cancellationToken);

            // Create or update subscription with forwarding
            var subscriptionName = $"{_queueName}-sub";
            var createSubscriptionOptions = new CreateSubscriptionOptions(TopicName, subscriptionName)
            {
                ForwardTo = _queueName
            };

            try
            {
                if (await adminClient.SubscriptionExistsAsync(TopicName, subscriptionName, cancellationToken))
                {
                    await adminClient.DeleteSubscriptionAsync(TopicName, subscriptionName, cancellationToken);
                }

                logger.LogInformation(
                    "Creating subscription {SubscriptionName} with forwarding to {QueueName}", 
                    subscriptionName, _queueName);
                    
                await adminClient.CreateSubscriptionAsync(createSubscriptionOptions, cancellationToken);

                // Create a rule for each message type
                for (var i = 0; i < _messageTypeFilters.Length; i++)
                {
                    var messageType = _messageTypeFilters[i];
                    var ruleName = $"MessageTypeFilter_{i}";
                    var ruleOptions = new CreateRuleOptions(
                        ruleName,
                        new SqlRuleFilter($"sys.MessageType LIKE '%{messageType}%'"));

                    await adminClient.CreateRuleAsync(TopicName, subscriptionName, ruleOptions, cancellationToken);
                    logger.LogInformation("Created rule {RuleName} for message type {MessageType}", 
                        ruleName, messageType);
                }

                // Delete the default rule if it exists
                try
                {
                    await adminClient.DeleteRuleAsync(TopicName, subscriptionName, "$Default", cancellationToken);
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
                {
                    // Rule doesn't exist, that's fine
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error setting up subscription {SubscriptionName}", subscriptionName);
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing topic: {TopicName}", TopicName);
            throw;
        }
    }

    private async Task CreateBundleTopicIfNecessary(CancellationToken cancellationToken)
    {
        if (!await adminClient.TopicExistsAsync(TopicName, cancellationToken))
        {
            try
            {
                logger.LogInformation("Creating topic: {TopicName}", TopicName);
                await adminClient.CreateTopicAsync(TopicName, cancellationToken);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                logger.LogInformation("Topic {TopicName} was created by another instance", TopicName);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
