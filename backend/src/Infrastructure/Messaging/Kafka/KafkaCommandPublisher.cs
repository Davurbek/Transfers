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

        _logger.LogInformation("KafkaCommandPublisher initializing with BootstrapServers={Bootstrap}, ClientId={ClientId}",
            _options.BootstrapServers, $"{_options.ClientId}-producer");

        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = $"{_options.ClientId}-producer",
        };
        _producer = new ProducerBuilder<string, string>(config).Build();

        _logger.LogInformation("KafkaCommandPublisher initialized successfully, target topic: {CommandsTopic}",
            _options.CommandsTopic);
    }

    public async Task PublishAsync(TransferCommand command, CancellationToken ct = default)
    {
        var key = command.CommandId;
        var value = JsonSerializer.Serialize(command, typeof(TransferCommand), JsonOpts);

        _logger.LogInformation(
            "Publishing command: Type={CommandType}, CommandId={CommandId}, IssuedBy={IssuedBy}, Size={Size} bytes",
            command.GetType().Name, command.CommandId, command.IssuedByUser, value.Length);

        var result = await _producer.ProduceAsync(
            _options.CommandsTopic,
            new Message<string, string>
            {
                Key = key,
                Value = value,
                Headers = new Headers
                {
                    new Header("command-type", System.Text.Encoding.UTF8.GetBytes(command.GetType().Name)),
                    new Header("issued-by", System.Text.Encoding.UTF8.GetBytes(command.IssuedByUser)),
                    new Header("content-type", System.Text.Encoding.UTF8.GetBytes("application/json")),
                },
            },
            ct);

        _logger.LogInformation(
            "Command {Type} published to Kafka topic '{Topic}' | Partition={Partition} | Offset={Offset} | Key={Key} | Status=Success",
            command.GetType().Name, _options.CommandsTopic, result.Partition, result.Offset, key);
    }

    public void Dispose()
    {
        _logger.LogInformation("KafkaCommandPublisher disposing, flushing producer...");
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        _logger.LogInformation("KafkaCommandPublisher disposed");
    }
}
