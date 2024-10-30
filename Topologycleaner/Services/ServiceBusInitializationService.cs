using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace TopologyCleaner.Services;

public class ServiceBusInitializationService(
    ServiceBusAdministrationClient adminClient,
    IHostApplicationLifetime hostLifetime,
    ILogger<ServiceBusInitializationService> logger)
    : IHostedService
{
    private readonly ServiceBusAdministrationClient _adminClient = adminClient;
    private readonly ILogger<ServiceBusInitializationService> _logger = logger;
    private readonly IHostApplicationLifetime _hostLifetime = hostLifetime;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleaning out the Service Bus namespace...");

        try
        {
            await DeleteAllQueuesAsync(cancellationToken);
            await DeleteAllTopicsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean up the Service Bus namespace.");
        }

        _logger.LogInformation("Service Bus namespace cleanup completed. Shutting down host.");
        _hostLifetime.StopApplication();

    }

    private async Task DeleteAllQueuesAsync(CancellationToken cancellationToken)
    {
        await foreach (var queue in _adminClient.GetQueuesAsync(cancellationToken))
        {
            await ExecuteWithRetry(async () =>
            {
                _logger.LogInformation("Deleting queue: {QueueName}", queue.Name);
                await _adminClient.DeleteQueueAsync(queue.Name, cancellationToken);
            });
        }
    }

    private async Task DeleteAllTopicsAsync(CancellationToken cancellationToken)
    {
        await foreach (var topic in _adminClient.GetTopicsAsync(cancellationToken))
        {
            await ExecuteWithRetry(async () =>
            {
                _logger.LogInformation("Deleting topic: {TopicName}", topic.Name);
                await _adminClient.DeleteTopicAsync(topic.Name, cancellationToken);
            });
        }
    }

    private async Task ExecuteWithRetry(Func<Task> action, int maxRetries = 5)
    {
        int retryCount = 0;
        TimeSpan delay = TimeSpan.FromSeconds(2);

        while (true)
        {
            try
            {
                await action();
                break;
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceBusy && retryCount < maxRetries)
            {
                retryCount++;
                _logger.LogWarning("Service is throttling. Waiting {Delay}s before retry {RetryCount}/{MaxRetries}.", delay.TotalSeconds, retryCount, maxRetries);
                await Task.Delay(delay);
                delay *= 2; // Exponential backoff
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}