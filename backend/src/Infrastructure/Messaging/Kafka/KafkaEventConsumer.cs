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

        _logger.LogInformation("Kafka consumer started, listening to {Topic}", _options.EventsTopic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    await ProcessMessageWithRetryAsync(result, stoppingToken);
                    consumer.Commit(result);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            consumer.Close();
            _logger.LogInformation("Kafka consumer stopped");
        }
    }

    private async Task ProcessMessageWithRetryAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await ProcessMessageAsync(result, ct);
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex,
                    "Failed to process event at offset {Offset} (attempt {Attempt}/{MaxRetries}), retrying...",
                    result.Offset, attempt, MaxRetries);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process event at offset {Offset} after {MaxRetries} attempts, sending to DLQ",
                    result.Offset, MaxRetries);
                await SendToDlqAsync(result, ct);
                return;
            }
        }
    }

    private async Task ProcessMessageAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        var @event = JsonSerializer.Deserialize<TransferEvent>(result.Message.Value, JsonOpts);
        if (@event is null)
        {
            _logger.LogWarning("Null event deserialized from topic {Topic} offset {Offset}",
                result.Topic, result.Offset);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var projector = scope.ServiceProvider.GetRequiredService<IEventProjector>();
        await projector.ProjectAsync(@event, ct);

        _logger.LogInformation("Projected event {Type} at offset {Offset}",
            @event.GetType().Name, result.Offset);
    }

    private async Task SendToDlqAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        try
        {
            using var producer = new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = _options.BootstrapServers,
            }).Build();

            await producer.ProduceAsync(_options.DlqTopic, new Message<string, string>
            {
                Key = result.Message.Key,
                Value = result.Message.Value,
                Headers = new Headers
                {
                    new Header("original-topic", System.Text.Encoding.UTF8.GetBytes(result.Topic)),
                    new Header("original-offset", System.Text.Encoding.UTF8.GetBytes(result.Offset.ToString())),
                    new Header("original-partition", System.Text.Encoding.UTF8.GetBytes(result.Partition.ToString())),
                },
            }, ct);

            _logger.LogWarning("Event at offset {Offset} moved to DLQ topic {DlqTopic}",
                result.Offset, _options.DlqTopic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send event to DLQ at offset {Offset}", result.Offset);
        }
    }
}
