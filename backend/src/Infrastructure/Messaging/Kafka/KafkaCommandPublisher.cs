using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.Outbox.Interfaces;

namespace Universal.Transfers.Infrastructure.Messaging.Kafka;

public sealed class KafkaCommandPublisher : ICommandPublisher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaCommandPublisher> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public KafkaCommandPublisher(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> options,
        ILogger<KafkaCommandPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;

        _logger.LogInformation("KafkaCommandPublisher (Outbox mode) initialized, target topic: {CommandsTopic}",
            _options.CommandsTopic);
    }

    public async Task PublishAsync(TransferCommand command, CancellationToken ct = default)
    {
        var value = JsonSerializer.Serialize(command, typeof(TransferCommand), JsonOpts);

        _logger.LogInformation(
            "Writing command to outbox: Type={CommandType}, CommandId={CommandId}, IssuedBy={IssuedBy}, Size={Size} bytes",
            command.GetType().Name, command.CommandId, command.IssuedByUser, value.Length);

        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();

        var outboxMessage = new Domain.Outbox.Entities.OutboxMessage
        {
            EventType = command.GetType().FullName!,
            AggregateId = command.CommandId,
            Payload = value,
            CreatedAt = DateTime.UtcNow,
        };

        outboxMessage.SetHeader("command-type", command.GetType().Name);
        outboxMessage.SetHeader("issued-by", command.IssuedByUser);

        await outboxRepo.AddAsync(outboxMessage, ct);
        await outboxRepo.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Command written to outbox | Id={OutboxId} | Type={Type} | AggregateId={AggregateId}",
            outboxMessage.Id, command.GetType().Name, command.CommandId);
    }
}
