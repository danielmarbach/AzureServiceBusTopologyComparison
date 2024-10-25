using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    private const string BundleTopicName = "bundle-1";

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
            case "MassTransit":
                await CreateMassTransitTopology(cancellationToken);
                break;
        }
    }

    private async Task CreateInputQueue(CancellationToken cancellationToken)
    {
        // Queue initialization
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
        await CreateTopic(BundleTopicName, cancellationToken);
    }

    private async Task InitializeSqlFilterTopology(CancellationToken cancellationToken)
    {
        await CreateTopic(BundleTopicName, cancellationToken);
    }

    private async Task CreateMassTransitTopology(CancellationToken cancellationToken)
    {
        for (var i = 0; i < _options.NumberOfMessages; i++)
        {
            var messageType = string.Format(_options.MessageTypeTemplate, i);
            // Split the message type into subtypes and create a topic for each
            var splitValues = messageType.Split([';'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var subtype in splitValues)
            {
                await CreateTopic(subtype.Trim(), cancellationToken);
            }
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