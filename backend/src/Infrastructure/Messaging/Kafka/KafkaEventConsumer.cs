using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Universal.Transfers.Application.Messaging;

namespace Universal.Transfers.Infrastructure.Messaging.Kafka;

public sealed class KafkaEventConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<KafkaEventConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOpts = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            GroupId = options.Value.GroupId,
            ClientId = options.Value.ClientId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(options.Value.EventsTopic);

        logger.LogInformation("Kafka consumer started, listening to {Topic}", options.Value.EventsTopic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    await ProcessMessageAsync(result, stoppingToken);
                    consumer.Commit(result);
                }
                catch (ConsumeException ex)
                {
                    logger.LogError(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
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
            logger.LogInformation("Kafka consumer stopped");
        }
    }

    private async Task ProcessMessageAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        var @event = JsonSerializer.Deserialize<TransferEvent>(result.Message.Value, JsonOpts);
        if (@event is null)
        {
            logger.LogWarning("Null event deserialized from topic {Topic} offset {Offset}",
                result.Topic, result.Offset);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var projector = scope.ServiceProvider.GetRequiredService<IEventProjector>();
        await projector.ProjectAsync(@event, ct);

        logger.LogInformation("Projected event {Type} at offset {Offset}",
            @event.GetType().Name, result.Offset);
    }
}
