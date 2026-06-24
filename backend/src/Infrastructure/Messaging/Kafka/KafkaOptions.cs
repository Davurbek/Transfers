namespace Universal.Transfers.Infrastructure.Messaging.Kafka;

public class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";
    public string CommandsTopic { get; set; } = "transfers-commands";
    public string EventsTopic { get; set; } = "transfers-events";
    public string GroupId { get; set; } = "transfers-dashboard";
    public string ClientId { get; set; } = "transfers-dashboard";
    public string DlqTopic { get; set; } = "transfers-events-dlq";
}
