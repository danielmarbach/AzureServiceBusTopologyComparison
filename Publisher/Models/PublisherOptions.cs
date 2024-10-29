namespace Publisher.Models;

public class PublisherOptions
{
    public const string ConfigurationSection = "Publisher";
    public string QueueName { get; set; } = "publisher-queue";
    public string TopologyType { get; set; } = "SqlFilter";

    public string MessageTypeTemplate { get; set; } =
        "Publisher.Messages.Test.TestEvent{0};Publisher.Messages.ITestEvent{0};Publisher.Messages.IMyOtherEvent;Publisher.Messages.IEvent";

    public EventRange EventRange { get; set; } = new();
}