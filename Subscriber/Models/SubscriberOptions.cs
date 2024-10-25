namespace Subscriber.Models;

public class SubscriberOptions
{
    public const string ConfigurationSection = "Subscriber";
    public string QueueName { get; set; } = "subscriber-queue";
    public string TopologyType { get; set; } = "SqlFilter";
    public string[] MessageTypeFilters { get; set; } = new[]
    {
        "Publisher.Messages.Test.TestMessageType",
        "Publisher.Messages.Other.OtherMessageType"
    };

    public int MaxConcurrentCalls { get; set; }
}
