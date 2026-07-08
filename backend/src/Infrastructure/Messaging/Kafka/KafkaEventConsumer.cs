using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Universal.Transfers.Application.Messaging;

namespace Universal.Transfers.Infrastructure.Messaging.Kafka;

public sealed class KafkaEventConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaEventConsumer> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new();
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
        _logger.LogInformation(
            "KafkaEventConsumer starting | BootstrapServers={Bootstrap} | GroupId={Group} | ClientId={Client} | EventsTopic={Topic} | DlqTopic={Dlq}",
            _options.BootstrapServers, _options.GroupId, _options.ClientId, _options.EventsTopic, _options.DlqTopic);

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            ClientId = _options.ClientId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_options.EventsTopic);

        _logger.LogInformation("Kafka consumer started, subscribed to topic '{Topic}' and waiting for messages", _options.EventsTopic);

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
                    result.Offset, MaxRetries, _options.DlqTopic);
                await SendToDlqAsync(result, ct);
                return;
            }
        }
    }

    private async Task ProcessMessageAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        _logger.LogDebug("Deserializing event at offset {Offset}: {Value}", result.Offset, result.Message.Value);

        var @event = JsonSerializer.Deserialize<TransferEvent>(result.Message.Value, JsonOpts);
        if (@event is null)
        {
            _logger.LogWarning("Null event deserialized from topic '{Topic}' offset {Offset} - raw: {Raw}",
                result.Topic, result.Offset, result.Message.Value);
            return;
        }

        _logger.LogInformation("Projecting event | Type={Type} | Offset={Offset} | EventData={EventData}",
            @event.GetType().Name, result.Offset,
            JsonSerializer.Serialize(@event, JsonOpts));

        using var scope = _scopeFactory.CreateScope();
        var projector = scope.ServiceProvider.GetRequiredService<IEventProjector>();
        await projector.ProjectAsync(@event, ct);

        _logger.LogInformation("Event {Type} successfully projected at offset {Offset}", @event.GetType().Name, result.Offset);
    }

    private async Task SendToDlqAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        _logger.LogWarning("Moving event to DLQ | OriginalTopic={Topic} | Offset={Offset} | Partition={Partition} | DLQ={DlqTopic}",
            result.Topic, result.Offset, result.Partition, _options.DlqTopic);

        try
        {
            using var producer = new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = _options.BootstrapServers,
            }).Build();

            var dlqResult = await producer.ProduceAsync(_options.DlqTopic, new Message<string, string>
            {
                Key = result.Message.Key,
                Value = result.Message.Value,
                Headers = new Headers
                {
                    new Header("original-topic", System.Text.Encoding.UTF8.GetBytes(result.Topic)),
                    new Header("original-offset", System.Text.Encoding.UTF8.GetBytes(result.Offset.ToString())),
                    new Header("original-partition", System.Text.Encoding.UTF8.GetBytes(result.Partition.ToString())),
                    new Header("dlq-timestamp", System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O"))),
                },
            }, ct);

            _logger.LogWarning(
                "Event moved to DLQ | DLQTopic={DlqTopic} | DLQPartition={Partition} | DLQOffset={Offset} | OriginalOffset={OriginalOffset}",
                _options.DlqTopic, dlqResult.Partition, dlqResult.Offset, result.Offset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CRITICAL: Failed to send event to DLQ topic '{DlqTopic}' - event at original offset {Offset} may be lost",
                _options.DlqTopic, result.Offset);
        }
    }
}
