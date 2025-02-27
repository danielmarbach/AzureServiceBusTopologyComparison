namespace Publisher.Models;

public class PublisherOptions
{
    public const string ConfigurationSection = "Publisher";
    public string QueueName { get; set; } = "publisher-queue";
    public string TopologyType { get; set; } = "SqlFilter";

    public string BundleTopicName { get; set; } = "bundle-1";

    public string? SendDestination { get; set; } = null;

    public int ThroughputPerMinute { get; set; } = 10;

    public string MessageTypesTemplate { get; set; } =
        "Publisher.Messages.Test.TestEvent{0};Publisher.Messages.ITestEvent{0};Publisher.Messages.IMyOtherEvent;Publisher.Messages.IEvent";

    public EventRange EventRange { get; set; } = new();
}