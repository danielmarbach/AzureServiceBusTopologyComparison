namespace Publisher.Models;

public class PublisherOptions
{
    public const string ConfigurationSection = "Publisher";
    
    public string QueueName { get; set; } = "publisher-queue";
    public string TopologyType { get; set; } = "SqlFilter";
    public string[] MessageTypes { get; set; } = new[]
    {
        "Publisher.Messages.Test.TestMessageType",
        "Publisher.Messages.OtherOtherMessageType"
    };
}
