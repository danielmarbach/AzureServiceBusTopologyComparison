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
        logger.LogInformation("Creating topology: {TopologyType}", _options.TopologyType);
        switch (_options.TopologyType)
        {
            case "SqlFilter":
                await InitializeSqlFilterTopology(cancellationToken);
                break;
            case "CorrelationFilter":
                await InitializeCorrelationFilterTopology(cancellationToken);
                break;
            case "MassTransit":
                await CreateMassTransitTopology(cancellationToken);
                break;
            case "SNS":
                await CreateSNSTopology(cancellationToken);
                break;
        }
    }

    private async Task InitializeCorrelationFilterTopology(CancellationToken cancellationToken)
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
            var messageType = string.Format(_options.MessageTypeTemplate, i);
            // Split the message type into subtypes and create a topic for each
            var splitValues = messageType.Split( new char[';'], StringSplitOptions.RemoveEmptyEntries);
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
            var messageType = string.Format(_options.MessageTypeTemplate, i);
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
                await adminClient.CreateTopicAsync(topicName, cancellationToken);
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