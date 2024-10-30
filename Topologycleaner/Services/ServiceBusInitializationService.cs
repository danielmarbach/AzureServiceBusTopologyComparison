using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace TopologyCleaner.Services;

public class ServiceBusInitializationService(
    ServiceBusAdministrationClient adminClient,
    IHostApplicationLifetime hostLifetime,
    ILogger<ServiceBusInitializationService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Cleaning out the Service Bus namespace...");

        try
        {
            await Task.WhenAll(
                DeleteAllQueuesAsync(cancellationToken),
                DeleteAllTopicsAsync(cancellationToken)
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clean up the Service Bus namespace.");
        }

        logger.LogInformation("Service Bus namespace cleanup completed. Shutting down host.");
        hostLifetime.StopApplication();

    }

    private async Task DeleteAllQueuesAsync(CancellationToken cancellationToken)
    {
        var queues = adminClient.GetQueuesAsync(cancellationToken);
        await Parallel.ForEachAsync(queues, cancellationToken, async (queue, ct) =>
        {
            await ExecuteWithRetry(async () =>
            {
                logger.LogInformation("Deleting queue: {QueueName}", queue.Name);
                await adminClient.DeleteQueueAsync(queue.Name, ct);
            }, cancellationToken: ct);
        });
    }

    private async Task DeleteAllTopicsAsync(CancellationToken cancellationToken)
    {
        var topics = adminClient.GetTopicsAsync(cancellationToken);
        await Parallel.ForEachAsync(topics, cancellationToken, async (topic, ct) =>
        {
            await ExecuteWithRetry(async () =>
            {
                logger.LogInformation("Deleting topic: {TopicName}", topic.Name);
                await adminClient.DeleteTopicAsync(topic.Name, ct);
            }, cancellationToken: ct);
        });
    }

    private async Task ExecuteWithRetry(Func<Task> action, int maxRetries = 5, CancellationToken cancellationToken = default)
    {
        var retryCount = 0;
        var delay = TimeSpan.FromSeconds(2);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await action();
                break;
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceBusy && retryCount < maxRetries)
            {
                retryCount++;
                logger.LogWarning("Service is throttling. Waiting {Delay}s before retry {RetryCount}/{MaxRetries}.", delay.TotalSeconds, retryCount, maxRetries);
                await Task.Delay(delay, cancellationToken);
                delay *= 2; // Exponential backoff
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}