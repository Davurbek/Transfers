using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Universal.Transfers.Domain.Outbox.Interfaces;
using Universal.Transfers.Infrastructure.Messaging.Kafka;

namespace Universal.Transfers.Infrastructure.Outbox.Services;

public sealed class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<OutboxPublisher> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private const int BatchSize = 20;
    private const int MaxRetries = 10;

    public OutboxPublisher(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> options,
        ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisher started | BootstrapServers={Bootstrap} | PollInterval={Interval}ms",
            _options.BootstrapServers, PollInterval.TotalMilliseconds);

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = $"{_options.ClientId}-outbox",
            EnableIdempotence = true,
            Acks = Acks.All,
        };

        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
        _logger.LogInformation("OutboxPublisher Kafka producer created with idempotent delivery");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PublishNextBatchAsync(producer, stoppingToken);
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OutboxPublisher encountered an error");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        finally
        {
            _logger.LogInformation("OutboxPublisher flushing producer before shutdown...");
            producer.Flush(TimeSpan.FromSeconds(5));
            _logger.LogInformation("OutboxPublisher stopped");
        }
    }

    private async Task PublishNextBatchAsync(IProducer<string, string> producer, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();

        var messages = await outboxRepo.GetUnprocessedMessagesAsync(BatchSize, ct);
        if (messages.Count == 0)
            return;

        _logger.LogInformation("OutboxPublisher publishing {Count} messages", messages.Count);

        foreach (var msg in messages)
        {
            try
            {
                var topic = ResolveTopic(msg.EventType);

                _logger.LogDebug("Publishing outbox message {Id} | Type={Type} | Topic={Topic} | AggregateId={AggregateId} | Retry={Retry}",
                    msg.Id, msg.EventType, topic, msg.AggregateId, msg.RetryCount);

                var kafkaHeaders = new Headers
                {
                    new Header("message-type", System.Text.Encoding.UTF8.GetBytes(msg.EventType)),
                    new Header("outbox-message-id", System.Text.Encoding.UTF8.GetBytes(msg.Id.ToString())),
                    new Header("content-type", System.Text.Encoding.UTF8.GetBytes("application/json")),
                    new Header("idempotency-key", System.Text.Encoding.UTF8.GetBytes(msg.Id.ToString())),
                };

                var result = await producer.ProduceAsync(topic, new Message<string, string>
                {
                    Key = msg.AggregateId,
                    Value = msg.Payload,
                    Headers = kafkaHeaders,
                }, ct);

                await outboxRepo.MarkProcessedAsync(msg.Id, ct);
                _logger.LogInformation("Outbox message {Id} published | Topic={Topic} | Partition={Partition} | Offset={Offset}",
                    msg.Id, result.Topic, result.Partition, result.Offset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish outbox message {Id}", msg.Id);

                await outboxRepo.IncrementRetryAsync(msg.Id, ct);
                if (msg.RetryCount + 1 >= MaxRetries)
                {
                    await outboxRepo.MarkFailedAsync(msg.Id, $"Max retries ({MaxRetries}) exceeded: {ex.Message}", ct);
                    _logger.LogWarning("Outbox message {Id} moved to failed state after {MaxRetries} retries", msg.Id, MaxRetries);
                }
            }
        }

        await outboxRepo.SaveChangesAsync(ct);
    }

    private string ResolveTopic(string eventTypeName)
    {
        var shortName = eventTypeName.Contains('.')
            ? eventTypeName.Split('.').Last()
            : eventTypeName;

        if (_options.Topics.TryGetValue(shortName, out var topic) && !string.IsNullOrWhiteSpace(topic))
            return topic;

        return _options.CommandsTopic;
    }
}
