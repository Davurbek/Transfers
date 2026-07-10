using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Universal.Transfers.Domain.DeadLetter.Interfaces;
using Universal.Transfers.Infrastructure.Messaging.Kafka;

namespace Universal.Transfers.Infrastructure.DeadLetter.Services;

public sealed class DeadLetterReplayService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<DeadLetterReplayService> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 10;

    public DeadLetterReplayService(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> options,
        ILogger<DeadLetterReplayService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeadLetterReplayService started | PollInterval={Interval}s", PollInterval.TotalSeconds);

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = $"{_options.ClientId}-dlq-replay",
        };

        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ReplayNextBatchAsync(producer, stoppingToken);
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "DeadLetterReplayService encountered an error");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
        }
        finally
        {
            _logger.LogInformation("DeadLetterReplayService stopped");
        }
    }

    private async Task ReplayNextBatchAsync(IProducer<string, string> producer, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dlqRepo = scope.ServiceProvider.GetRequiredService<IDeadLetterRepository>();

        var messages = await dlqRepo.GetUnreplayedMessagesAsync(BatchSize, ct);
        if (messages.Count == 0)
            return;

        _logger.LogInformation("DeadLetterReplayService replaying {Count} messages", messages.Count);

        foreach (var msg in messages)
        {
            _logger.LogInformation("Replaying DLQ message {Id} | OriginalTopic={Topic} | Offset={Offset} | Retry={Retry}/{MaxRetries}",
                msg.Id, msg.OriginalTopic, msg.OriginalOffset, msg.RetryCount, msg.MaxRetries);

            try
            {
                var result = await producer.ProduceAsync(msg.OriginalTopic, new Message<string, string>
                {
                    Key = msg.MessageKey,
                    Value = msg.Payload,
                    Headers = new Headers
                    {
                        new Header("idempotency-key", System.Text.Encoding.UTF8.GetBytes(msg.Id.ToString())),
                        new Header("replayed-from-dlq", System.Text.Encoding.UTF8.GetBytes("true")),
                        new Header("dlq-message-id", System.Text.Encoding.UTF8.GetBytes(msg.Id.ToString())),
                        new Header("replayed-at", System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O"))),
                        new Header("original-offset", System.Text.Encoding.UTF8.GetBytes(msg.OriginalOffset.ToString())),
                    },
                }, ct);

                await dlqRepo.MarkReplayedAsync(msg.Id, ct);
                _logger.LogInformation("DLQ message {Id} replayed successfully | Topic={Topic} | Offset={Offset}",
                    msg.Id, result.Topic, result.Offset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to replay DLQ message {Id}", msg.Id);
                await dlqRepo.IncrementRetryAsync(msg.Id, ex.Message, ct);
            }
        }

        await dlqRepo.SaveChangesAsync(ct);
    }
}
