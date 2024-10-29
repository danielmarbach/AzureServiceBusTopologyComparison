namespace Subscriber.Models;

public class SubscriberOptions
{
    public const string ConfigurationSection = "Subscriber";
    public string QueueName { get; set; } = "subscriber-queue";
    public string TopologyType { get; set; } = "SqlFilter";

    public string MessageTypeTemplate { get; set; } =
        "Publisher.Messages.Test.TestEvent{0};Publisher.Messages.ITestEvent{0};Publisher.Messages.IMyOtherEvent;Publisher.Messages.IEvent";

    public int EventRangeBegin { get; set; } = 0;

    public int EventRangeEnd { get; set; } = 3;

    public int MaxConcurrentCalls { get; set; } = Environment.ProcessorCount;
}