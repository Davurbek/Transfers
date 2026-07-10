using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using Universal.Transfers.Domain.DeadLetter.Entities;
using Universal.Transfers.Domain.DeadLetter.Interfaces;
using Universal.Transfers.Infrastructure.Messaging.Kafka;

namespace Universal.Transfers.Infrastructure.DeadLetter.Services;

public sealed class DeadLetterService
{
    private readonly IDeadLetterRepository _repo;
    private readonly KafkaOptions _options;
    private readonly ILogger<DeadLetterService> _logger;

    public DeadLetterService(
        IDeadLetterRepository repo,
        IOptions<KafkaOptions> options,
        ILogger<DeadLetterService> logger)
    {
        _repo = repo;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendToDlqAsync(
        ConsumeResult<string, string> result,
        string failureReason,
        CancellationToken ct = default)
    {
        var dlqTopic = $"{result.Topic}_error";
        _logger.LogWarning("Moving event to DLQ | OriginalTopic={Topic} | Offset={Offset} | Partition={Partition} | Reason={Reason}",
            result.Topic, result.Offset, result.Partition, failureReason);

        var dlqMessage = new DeadLetterMessage
        {
            OriginalTopic = result.Topic,
            OriginalOffset = result.Offset,
            OriginalPartition = result.Partition,
            MessageKey = result.Message.Key ?? "",
            Payload = result.Message.Value,
            FailureReason = failureReason,
        };

        await _repo.AddAsync(dlqMessage, ct);
        await _repo.SaveChangesAsync(ct);

        _logger.LogInformation("DLQ message stored locally | Id={DlqId} | Topic={Topic} | Offset={Offset}",
            dlqMessage.Id, dlqTopic, result.Offset);

        try
        {
            using var producer = new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = _options.BootstrapServers,
            }).Build();

            var originalIdempotencyKey = result.Message.Headers?
                .FirstOrDefault(h => h.Key == "idempotency-key")?
                .GetValueBytes();

            var headers = new Headers
            {
                new Header("original-topic", Encoding.UTF8.GetBytes(result.Topic)),
                new Header("original-offset", Encoding.UTF8.GetBytes(result.Offset.ToString())),
                new Header("original-partition", Encoding.UTF8.GetBytes(result.Partition.ToString())),
                new Header("dlq-timestamp", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O"))),
                new Header("failure-reason", Encoding.UTF8.GetBytes(failureReason)),
                new Header("dlq-message-id", Encoding.UTF8.GetBytes(dlqMessage.Id.ToString())),
            };

            if (originalIdempotencyKey is not null)
                headers.Add("idempotency-key", originalIdempotencyKey);

            var dlqResult = await producer.ProduceAsync(dlqTopic, new Message<string, string>
            {
                Key = result.Message.Key ?? string.Empty,
                Value = result.Message.Value,
                Headers = headers,
            }, ct);

            _logger.LogWarning(
                "Event moved to Kafka DLQ | DLQTopic={DlqTopic} | DLQPartition={Partition} | DLQOffset={Offset} | DlqId={DlqId}",
                dlqTopic, dlqResult.Partition, dlqResult.Offset, dlqMessage.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send event to Kafka DLQ topic '{DlqTopic}' - stored locally with Id={DlqId}",
                dlqTopic, dlqMessage.Id);
        }
    }
}
