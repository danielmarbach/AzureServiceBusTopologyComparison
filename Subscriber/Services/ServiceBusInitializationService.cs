using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Options;
using Subscriber.Models;

namespace Subscriber.Services;

public class ServiceBusInitializationService(
    ServiceBusAdministrationClient adminClient,
    IOptions<SubscriberOptions> options,
    ILogger<ServiceBusInitializationService> logger)
    : IHostedService
{
    private readonly SubscriberOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await CreateInputQueue(cancellationToken);

        logger.LogInformation("Creating topology: {TopologyType}", _options.TopologyType);
        switch (_options.TopologyType)
        {
            case "SqlFilter":
                await InitializeSqlFilterTopology(cancellationToken);
                break;
            case "CorrelationFilter":
                await InitializeCorrelationFilterTopology(cancellationToken);
                break;
            case "CorrelationFilterWithoutInheritance":
                await InitializeCorrelationFilterWithoutInheritanceTopology(cancellationToken);
                break;
            case "MassTransit":
                await CreateMassTransitTopology(cancellationToken);
                break;
            case "SNS":
                await CreateSNSTopology(cancellationToken);
                break;
        }
    }

    private async Task CreateInputQueue(CancellationToken cancellationToken)
    {
        try
        {
            if (await adminClient.QueueExistsAsync(_options.QueueName, cancellationToken))
            {
                logger.LogInformation("Deleting existing queue: {QueueName}", _options.QueueName);
                await adminClient.DeleteQueueAsync(_options.QueueName, cancellationToken);
            }

            logger.LogInformation("Creating queue: {QueueName}", _options.QueueName);
            await adminClient.CreateQueueAsync(_options.QueueName, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing queue: {QueueName}", _options.QueueName);
            throw;
        }
    }

    private async Task InitializeCorrelationFilterTopology(CancellationToken cancellationToken)
    {
        await CreateTopic(_options.BundleTopicName, cancellationToken);

        // Create or update subscription with correlation filters
        var subscriptionName = $"{_options.QueueName}-sub";
        var createSubscriptionOptions = new CreateSubscriptionOptions(_options.BundleTopicName, subscriptionName)
        {
            ForwardTo = _options.QueueName
        };

        await CreateSubscription(createSubscriptionOptions, cancellationToken);

        // Create a correlation filter rule for each split message type
        foreach (var i in _options.EventRange.ToRange())
        {
            var messageType = string.Format(_options.MessageTypeTemplate, i);
            var hierarchyTypes = messageType.Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var hierarchyType in hierarchyTypes)
            {
                var hierarchyTypeTrimmed = hierarchyType.Trim();
                var ruleName = $"{hierarchyTypeTrimmed[..Math.Min(hierarchyTypeTrimmed.Length, 50)]}";
                var ruleOptions = new CreateRuleOptions(
                    ruleName,
                    new CorrelationRuleFilter
                    {
                        ApplicationProperties = { { hierarchyTypeTrimmed, true } }
                    });

                try
                {
                    await adminClient.CreateRuleAsync(_options.BundleTopicName, subscriptionName, ruleOptions,
                        cancellationToken);
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    // Rule exists already, that's fine
                }

                logger.LogInformation("Created correlation rule {RuleName} for message type {MessageType}",
                    ruleName, messageType);
            }
        }

        // Delete the default rule if it exists
        try
        {
            await adminClient.DeleteRuleAsync(_options.BundleTopicName, subscriptionName, "$Default",
                cancellationToken);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            // Rule doesn't exist, that's fine
        }
    }

    private async Task InitializeCorrelationFilterWithoutInheritanceTopology(CancellationToken cancellationToken)
    {
        await CreateTopic(_options.BundleTopicName, cancellationToken);

        // Create or update subscription with correlation filters
        var subscriptionName = $"{_options.QueueName}-sub";
        var createSubscriptionOptions = new CreateSubscriptionOptions(_options.BundleTopicName, subscriptionName)
        {
            ForwardTo = _options.QueueName
        };

        await CreateSubscription(createSubscriptionOptions, cancellationToken);

        // Create a correlation filter rule for each split message type
        foreach (var i in _options.EventRange.ToRange())
        {
            var messageType = string.Format(_options.MessageTypeTemplate, i);
            //Only the most concrete type is of interest
            var mostConcreteType = messageType.Split(';', StringSplitOptions.RemoveEmptyEntries).First().Trim();

            var ruleName = $"{mostConcreteType[..Math.Min(mostConcreteType.Length, 50)]}";
            var ruleOptions = new CreateRuleOptions(
                ruleName,
                new CorrelationRuleFilter
                {
                    ApplicationProperties = { { mostConcreteType, true } }
                });

            try
            {
                await adminClient.CreateRuleAsync(_options.BundleTopicName, subscriptionName, ruleOptions,
                    cancellationToken);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                // Rule exists already, that's fine
            }

            logger.LogInformation("Created correlation rule {RuleName} for message type {MessageType}",
                ruleName, messageType);
        }

        // Delete the default rule if it exists
        try
        {
            await adminClient.DeleteRuleAsync(_options.BundleTopicName, subscriptionName, "$Default",
                cancellationToken);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            // Rule doesn't exist, that's fine
        }
    }


    private async Task InitializeSqlFilterTopology(CancellationToken cancellationToken)
    {
        await CreateTopic(_options.BundleTopicName, cancellationToken);

        // Create or update subscription with forwarding
        var subscriptionName = $"{_options.QueueName}-sub";
        var createSubscriptionOptions = new CreateSubscriptionOptions(_options.BundleTopicName, subscriptionName)
        {
            ForwardTo = _options.QueueName
        };

        try
        {
            await CreateSubscription(createSubscriptionOptions, cancellationToken);

            // Create a rule for each message type
            foreach (var i in _options.EventRange.ToRange())
            {
                var messageType = string.Format(_options.MessageTypeTemplate, i);
                var hierarchyTypes = messageType.Split(';', StringSplitOptions.RemoveEmptyEntries);
                var ruleName = $"MessageTypeFilter_{i}";
                var ruleOptions = new CreateRuleOptions(
                    ruleName,
                    new SqlRuleFilter($"[MessageType] LIKE '%{hierarchyTypes[1]}%'"));

                await adminClient.CreateRuleAsync(_options.BundleTopicName, subscriptionName, ruleOptions,
                    cancellationToken);
                logger.LogInformation("Created rule {RuleName} for message type {MessageType}",
                    ruleName, messageType);
            }

            // Delete the default rule if it exists
            try
            {
                await adminClient.DeleteRuleAsync(_options.BundleTopicName, subscriptionName, "$Default",
                    cancellationToken);
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

    private async Task CreateMassTransitTopology(CancellationToken cancellationToken)
    {
        foreach (var i in _options.EventRange.ToRange())
        {
            var messageType = string.Format(_options.MessageTypeTemplate, i);
            // Split the message type into subtypes and create a topic for each
            var topicNames = messageType.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(subtype => subtype.Trim())
                .ToList();
            // The assumption here is for simulation reasons that the messages inherit from each other
            // from left to right and the most generic one is the last one. No fancy multi-inheritance here.
            foreach (var topicName in topicNames)
            {
                await CreateTopic(topicName, cancellationToken);
            }

            for (var j = 0; j < topicNames.Count; j++)
            {
                var topicName = topicNames[j];
                var forwardingDestination = j < topicNames.Count - 1
                    ? topicNames[j + 1]
                    : _options.QueueName;

                var subscriptionName = $"{forwardingDestination}-sub";
                var createSubscriptionOptions = new CreateSubscriptionOptions(topicName, subscriptionName)
                {
                    ForwardTo = forwardingDestination
                };

                await CreateSubscription(createSubscriptionOptions, cancellationToken);
            }
        }
    }

    private async Task CreateSNSTopology(CancellationToken cancellationToken)
    {
        //Creates multiple subscriptions, one for each concrete event.
        //This is similar to what SNS does when using the mapping like this one:
        //MapEvent<TestEvent0>("IMyOtherEvent");
        //MapEvent<TestEvent1>("IMyOtherEvent"); 
        //MapEvent<TestEvent2>("IMyOtherEvent"); 
        //MapEvent<TestEvent3>("IMyOtherEvent"); 
        foreach (var i in _options.EventRange.ToRange())
        {
            var messageType = string.Format(_options.MessageTypeTemplate, i);
            //Only topic for the most concrete type exists
            var mostConcreteType = messageType.Split(';', StringSplitOptions.RemoveEmptyEntries).First().Trim();

            await CreateTopic(mostConcreteType, cancellationToken);

            var forwardingDestination = _options.QueueName;
            var subscriptionName = $"{forwardingDestination}-sub";

            var createSubscriptionOptions = new CreateSubscriptionOptions(mostConcreteType, subscriptionName)
            {
                ForwardTo = forwardingDestination
            };

            await CreateSubscription(createSubscriptionOptions, cancellationToken);
        }
    }

    private async Task CreateTopic(string topicName, CancellationToken cancellationToken)
    {
        if (!await adminClient.TopicExistsAsync(topicName, cancellationToken))
        {
            try
            {
                logger.LogInformation("Creating topic: {TopicName}", topicName);
                await adminClient.CreateTopicAsync(new CreateTopicOptions(topicName)
                {
                    MaxSizeInMegabytes = 5*1024,
                }, cancellationToken);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                logger.LogInformation("Topic {TopicName} was created by another instance", topicName);
            }
        }
        else
        {
            logger.LogInformation("Topic already exists: {TopicName}", topicName);
        }
    }

    private async Task CreateSubscription(CreateSubscriptionOptions subscriptionOptions,
        CancellationToken cancellationToken)
    {
        if (!await adminClient.SubscriptionExistsAsync(subscriptionOptions.TopicName,
                subscriptionOptions.SubscriptionName, cancellationToken))
        {
            try
            {
                logger.LogInformation("Creating subscription {SubscriptionName} on {TopicName}",
                    subscriptionOptions.TopicName, subscriptionOptions.SubscriptionName);
                await adminClient.CreateSubscriptionAsync(subscriptionOptions, cancellationToken);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                logger.LogInformation("Subscription {SubscriptionName} on {TopicName} was created by another instance",
                    subscriptionOptions.TopicName, subscriptionOptions.SubscriptionName);
            }
        }
        else
        {
            logger.LogInformation("Subscription {SubscriptionName} on {TopicName} already exists.",
                subscriptionOptions.TopicName, subscriptionOptions.SubscriptionName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}