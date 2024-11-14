using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Options;
using Publisher.Models;

namespace Publisher.Services;

public class ServiceBusInitializationService(
    ServiceBusAdministrationClient adminClient,
    IOptions<PublisherOptions> options,
    ILogger<ServiceBusInitializationService> logger)
    : IHostedService
{
    private readonly PublisherOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.SendDestination))
        {
            await CreateSendDestination(cancellationToken);
            return;
        }
        
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

    private async Task CreateSendDestination(CancellationToken cancellationToken)
    {
        logger.LogInformation("Send destination {SendDestination} was set and therefore the topology creation will be skipped.", _options.SendDestination);

        if (await adminClient.QueueExistsAsync(_options.SendDestination, cancellationToken))
        {
            logger.LogInformation("Queue already exists: {QueueName}", _options.SendDestination);
        }
        else
        {
            logger.LogInformation("Creating queue: {QueueName}", _options.SendDestination);
            try
            {
                await adminClient.CreateQueueAsync(_options.SendDestination, cancellationToken);
            }
            catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                logger.LogInformation("Queue {QueueName} was created by another instance", _options.SendDestination);
            }
        }
    }

    private async Task InitializeCorrelationFilterTopology(CancellationToken cancellationToken)
    {
        await CreateTopic(_options.BundleTopicName, cancellationToken);
    }
    
    private async Task InitializeCorrelationFilterWithoutInheritanceTopology(CancellationToken cancellationToken)
    {
        await CreateTopic(_options.BundleTopicName, cancellationToken);
    }

    private async Task InitializeSqlFilterTopology(CancellationToken cancellationToken)
    {
        await CreateTopic(_options.BundleTopicName, cancellationToken);
    }

    private async Task CreateMassTransitTopology(CancellationToken cancellationToken)
    {
        foreach (var i in _options.EventRange.ToRange())
        {
            var messageType = string.Format(_options.MessageTypesTemplate, i);
            // Split the message type into subtypes and create a topic for each
            var splitValues = messageType.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var subtype in splitValues)
            {
                await CreateTopic(subtype.Trim(), cancellationToken);
            }
        }
    }

    private async Task CreateSNSTopology(CancellationToken cancellationToken)
    {
        foreach (var i in _options.EventRange.ToRange())
        {
            var messageType = string.Format(_options.MessageTypesTemplate, i);
            //Only topic for the most concrete type exists
            var mostConcreteType = messageType.Split(';', StringSplitOptions.RemoveEmptyEntries).First().Trim();
            await CreateTopic(mostConcreteType, cancellationToken);
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

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}