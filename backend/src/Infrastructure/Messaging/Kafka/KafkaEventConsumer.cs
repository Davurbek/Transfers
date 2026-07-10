using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.Inbox.Entities;
using Universal.Transfers.Domain.Inbox.Interfaces;
using Universal.Transfers.Infrastructure.DeadLetter.Services;

namespace Universal.Transfers.Infrastructure.Messaging.Kafka;

public sealed class KafkaEventConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaEventConsumer> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private const int MaxRetries = 3;

    public KafkaEventConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> options,
        ILogger<KafkaEventConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var topics = _options.Topics.Values.Distinct().ToList();
        _logger.LogInformation(
            "KafkaEventConsumer starting | BootstrapServers={Bootstrap} | GroupId={Group} | ClientId={Client} | Topics={Topics} | DlqTopic={Dlq}",
            _options.BootstrapServers, _options.GroupId, _options.ClientId, string.Join(", ", topics), _options.DlqTopic);

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            ClientId = _options.ClientId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(topics);

        _logger.LogInformation("Kafka consumer started, subscribed to {Count} topics and waiting for messages", topics.Count);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);

                    _logger.LogInformation(
                        "Kafka message received | Topic={Topic} | Partition={Partition} | Offset={Offset} | Key={Key} | ValueSize={Size} bytes",
                        result.Topic, result.Partition, result.Offset, result.Message.Key, result.Message.Value.Length);

                    await ProcessMessageWithRetryAsync(result, stoppingToken);
                    consumer.Commit(result);

                    _logger.LogDebug("Kafka offset {Offset} committed successfully", result.Offset);
                }
                catch (ConsumeException ex) when (ex.Error.IsLocalError)
                {
                    _logger.LogError(ex, "Kafka local consume error: {Reason}", ex.Error.Reason);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error: {Reason} | Code={Code} | IsFatal={IsFatal}",
                        ex.Error.Reason, ex.Error.Code, ex.Error.IsFatal);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Kafka consumer cancellation requested, shutting down");
                    break;
                }
            }
        }
        finally
        {
            consumer.Close();
            _logger.LogInformation("Kafka consumer stopped and connection closed");
        }
    }

    private async Task ProcessMessageWithRetryAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await ProcessMessageAsync(result, ct);

                _logger.LogInformation(
                    "Event processing succeeded | Offset={Offset} | Attempt={Attempt} | Topic={Topic} | Partition={Partition}",
                    result.Offset, attempt, result.Topic, result.Partition);
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex,
                    "Event processing failed at offset {Offset} (attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms...",
                    result.Offset, attempt, MaxRetries, 100 * attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Event processing failed at offset {Offset} after {MaxRetries} attempts, routing to DLQ topic '{DlqTopic}'",
                    result.Offset, MaxRetries, $"{result.Topic}_error");
                await SendToDlqAsync(result, ct);
                return;
            }
        }
    }

    private async Task ProcessMessageAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        _logger.LogDebug("Deserializing event at offset {Offset}: {Value}", result.Offset, result.Message.Value);

        var transferEvent = DeserializeTransferEvent(result.Topic, result.Message.Value);
        if (transferEvent is null)
        {
            _logger.LogWarning("Null/unknown event deserialized from topic '{Topic}' offset {Offset} - raw: {Raw}",
                result.Topic, result.Offset, result.Message.Value.Length > 500 ? result.Message.Value[..500] : result.Message.Value);
            return;
        }

        var idempotencyKey = result.Message.Headers?
            .FirstOrDefault(h => h.Key == "idempotency-key")?
            .GetValueBytes();

        var key = idempotencyKey is not null
            ? System.Text.Encoding.UTF8.GetString(idempotencyKey)
            : result.Message.Key ?? $"{result.Topic}-{result.Offset}";

        using var scope = _scopeFactory.CreateScope();
        var processedRepo = scope.ServiceProvider.GetRequiredService<IProcessedMessageRepository>();

        var seen = await processedRepo.GetByIdempotencyKeyAsync(key, ct);
        if (seen is not null)
        {
            _logger.LogWarning("Skipping duplicate message. IdempotencyKey={Key} | ProcessedAt={ProcessedAt}", key, seen.ProcessedAt);
            return;
        }

        _logger.LogInformation("Projecting event | Type={Type} | Offset={Offset} | IdempotencyKey={Key}",
            transferEvent.GetType().Name, result.Offset, key);

        var projector = scope.ServiceProvider.GetRequiredService<IEventProjector>();
        await projector.ProjectAsync(transferEvent, ct);

        processedRepo.Add(ProcessedMessage.Create(key, transferEvent.GetType().FullName!, DateTime.UtcNow));
        await processedRepo.SaveChangesAsync(ct);

        _logger.LogInformation("Event {Type} projected and dedup recorded at offset {Offset}", transferEvent.GetType().Name, result.Offset);
    }

    private static string GetInternalRef(TransferEvent @event) => @event switch
    {
        TransactionInitiatedEvent e => e.InternalRef,
        TransactionCreditCompletedEvent e => e.InternalRef,
        TransactionCreditFailedEvent e => e.InternalRef,
        TransactionCreditFailedRetryEvent e => e.InternalRef,
        TransactionCreditRetryRequestedEvent e => e.InternalRef,
        TransactionRegistrationCompletedEvent e => e.InternalRef,
        TransactionRegistrationFailedRetryEvent e => e.InternalRef,
        TransactionRegistrationRetryRequestedEvent e => e.InternalRef,
        TransactionPausedEvent e => e.InternalRef,
        TransactionUnpausedEvent e => e.InternalRef,
        _ => "unknown",
    };

    private static DateTime GetOccurredOn(TransferEvent @event) => @event switch
    {
        TransactionInitiatedEvent e => e.OccurredOn,
        TransactionCreditCompletedEvent e => e.OccurredOn,
        TransactionCreditFailedEvent e => e.OccurredOn,
        TransactionCreditFailedRetryEvent e => e.OccurredOn,
        TransactionCreditRetryRequestedEvent e => e.OccurredOn,
        TransactionRegistrationCompletedEvent e => e.OccurredOn,
        TransactionRegistrationFailedRetryEvent e => e.OccurredOn,
        TransactionRegistrationRetryRequestedEvent e => e.OccurredOn,
        TransactionPausedEvent e => e.OccurredOn,
        TransactionUnpausedEvent e => e.OccurredOn,
        _ => DateTime.MinValue,
    };

    private TransferEvent? DeserializeTransferEvent(string topic, string payload)
    {
        var reverseMap = TopicToEventType;
        if (!reverseMap.TryGetValue(topic, out var eventTypeName))
        {
            _logger.LogWarning("No event type mapping found for topic '{Topic}'", topic);
            return null;
        }

        _logger.LogDebug("Deserializing event of type {EventType} from topic '{Topic}'", eventTypeName, topic);

        return eventTypeName switch
        {
            nameof(TransactionInitiatedEvent) =>
                JsonSerializer.Deserialize<TransactionInitiatedEvent>(payload, JsonOpts),
            nameof(TransactionCreditCompletedEvent) =>
                JsonSerializer.Deserialize<TransactionCreditCompletedEvent>(payload, JsonOpts),
            nameof(TransactionCreditFailedEvent) =>
                JsonSerializer.Deserialize<TransactionCreditFailedEvent>(payload, JsonOpts),
            nameof(TransactionCreditFailedRetryEvent) =>
                JsonSerializer.Deserialize<TransactionCreditFailedRetryEvent>(payload, JsonOpts),
            nameof(TransactionCreditRetryRequestedEvent) =>
                JsonSerializer.Deserialize<TransactionCreditRetryRequestedEvent>(payload, JsonOpts),
            nameof(TransactionRegistrationCompletedEvent) =>
                JsonSerializer.Deserialize<TransactionRegistrationCompletedEvent>(payload, JsonOpts),
            nameof(TransactionRegistrationFailedRetryEvent) =>
                JsonSerializer.Deserialize<TransactionRegistrationFailedRetryEvent>(payload, JsonOpts),
            nameof(TransactionRegistrationRetryRequestedEvent) =>
                JsonSerializer.Deserialize<TransactionRegistrationRetryRequestedEvent>(payload, JsonOpts),
            nameof(TransactionPausedEvent) =>
                JsonSerializer.Deserialize<TransactionPausedEvent>(payload, JsonOpts),
            nameof(TransactionUnpausedEvent) =>
                JsonSerializer.Deserialize<TransactionUnpausedEvent>(payload, JsonOpts),
            _ => null,
        };
    }

    private Dictionary<string, string>? _topicToEventType;
    private Dictionary<string, string> TopicToEventType =>
        _topicToEventType ??= _options.Topics.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    private async Task SendToDlqAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dlqService = scope.ServiceProvider.GetRequiredService<DeadLetterService>();
        await dlqService.SendToDlqAsync(result, $"Max retries exceeded", ct);
    }
}
