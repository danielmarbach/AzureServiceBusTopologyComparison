namespace Subscriber.Models;

public class SubscriberOptions
{
    public const string ConfigurationSection = "Subscriber";
    public string QueueName { get; set; } = "subscriber-queue";
    public string TopologyType { get; set; } = "SqlFilter";

    public string BundleTopicName { get; set; } = "bundle-1";

    public string MessageTypeTemplate { get; set; } =
        "Publisher.Messages.Test.TestEvent{0};Publisher.Messages.ITestEvent{0};Publisher.Messages.IMyOtherEvent;Publisher.Messages.IEvent";

    public EventRange EventRange { get; set; } = new();

    public int ThroughputPerMinute { get; set; } = 10;
}