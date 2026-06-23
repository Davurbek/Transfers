using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Universal.Transfers.Application.Messaging;

namespace Universal.Transfers.Infrastructure.Messaging.Kafka;

public sealed class KafkaCommandPublisher : ICommandPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaCommandPublisher> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
    };

    public KafkaCommandPublisher(
        IOptions<KafkaOptions> options,
        ILogger<KafkaCommandPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = $"{_options.ClientId}-producer",
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync(TransferCommand command, CancellationToken ct = default)
    {
        var key = command.CommandId;
        var value = JsonSerializer.Serialize(command, typeof(TransferCommand), JsonOpts);

        var result = await _producer.ProduceAsync(
            _options.CommandsTopic,
            new Message<string, string> { Key = key, Value = value },
            ct);

        _logger.LogInformation("Command {Type} published to Kafka [{Topic}] partition {Partition} offset {Offset}",
            command.GetType().Name, _options.CommandsTopic, result.Partition, result.Offset);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
