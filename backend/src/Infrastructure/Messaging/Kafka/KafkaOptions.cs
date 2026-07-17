using Universal.Transfers.Application.Messaging;

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

    public Dictionary<string, string> Topics { get; set; } = new()
    {
        [nameof(TransactionInitiatedEvent)] = "transaction.initiated",
        [nameof(TransactionPausedEvent)] = "transaction.paused",
        [nameof(TransactionUnpausedEvent)] = "transaction.unpaused",
        [nameof(TransactionUnpauseRequestedEvent)] = "transaction.unpause_requested",
        [nameof(TransactionCreditCompletedEvent)] = "transaction.credit_completed",
        [nameof(TransactionCreditFailedEvent)] = "transaction.credit_failed",
        [nameof(TransactionCreditFailedRetryEvent)] = "transaction.credit_failed_retry",
        [nameof(TransactionRegistrationCompletedEvent)] = "transaction.registration_completed",
        [nameof(TransactionRegistrationFailedRetryEvent)] = "transaction.registration_failed_retry",
    };
}
