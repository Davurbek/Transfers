using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Universal.Transfers.Application.Messaging;

namespace Universal.Transfers.Infrastructure.Messaging.Kafka;

public sealed class KafkaCommandPublisher(
    IOptions<KafkaOptions> options,
    ILogger<KafkaCommandPublisher> logger) : ICommandPublisher
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
    };

    public async Task PublishAsync(TransferCommand command, CancellationToken ct = default)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            ClientId = $"{options.Value.ClientId}-producer",
        };

        using var producer = new ProducerBuilder<string, string>(config).Build();

        var key = command.CommandId;
        var value = JsonSerializer.Serialize(command, typeof(TransferCommand), JsonOpts);

        var result = await producer.ProduceAsync(
            options.Value.CommandsTopic,
            new Message<string, string> { Key = key, Value = value },
            ct);

        logger.LogInformation("Command {Type} published to Kafka [{Topic}] partition {Partition} offset {Offset}",
            command.GetType().Name, options.Value.CommandsTopic, result.Partition, result.Offset);
    }
}
