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
    private readonly string _queueName = options.Value.QueueName;
    private readonly string _topologyType = options.Value.TopologyType;
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
        await CreateTopic(cancellationToken);
    }

    private async Task InitializeSqlFilterTopology(CancellationToken cancellationToken)
    {
        await CreateTopic(cancellationToken);
    }

    private async Task CreateTopic(CancellationToken cancellationToken)
    {
        // Topic initialization
        try
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing topic: {TopicName}", TopicName);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
