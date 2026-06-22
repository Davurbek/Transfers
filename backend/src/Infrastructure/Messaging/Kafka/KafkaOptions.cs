namespace Universal.Transfers.Infrastructure.Messaging.Kafka;

public class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; init; } = "localhost:9092";
    public string CommandsTopic { get; init; } = "transfers-commands";
    public string EventsTopic { get; init; } = "transfers-events";
    public string GroupId { get; init; } = "transfers-dashboard";
    public string ClientId { get; init; } = "transfers-dashboard";
}
