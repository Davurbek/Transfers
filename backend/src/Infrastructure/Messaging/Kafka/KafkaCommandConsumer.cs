using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.Transactions.Enums;
using Universal.Transfers.Domain.Transactions.Interfaces;

namespace Universal.Transfers.Infrastructure.Messaging.Kafka;

public sealed class KafkaCommandConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaCommandConsumer> _logger;
    private readonly IProducer<string, string> _producer;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions DeserializeOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public KafkaCommandConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> options,
        ILogger<KafkaCommandConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;

        _logger.LogInformation(
            "KafkaCommandConsumer initializing | BootstrapServers={Bootstrap} | GroupId={Group} | CommandsTopic={Topic} | EventsTopic={EventsTopic}",
            _options.BootstrapServers, $"{_options.GroupId}-commands", _options.CommandsTopic, _options.EventsTopic);

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = $"{_options.ClientId}-command-processor",
        };
        _producer = new ProducerBuilder<string, string>(producerConfig).Build();

        _logger.LogInformation("KafkaCommandConsumer initialized successfully");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KafkaCommandConsumer starting, subscribing to commands topic '{Topic}'", _options.CommandsTopic);

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = $"{_options.GroupId}-commands",
            ClientId = $"{_options.ClientId}-commands",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_options.CommandsTopic);

        _logger.LogInformation("KafkaCommandConsumer started, listening to topic '{Topic}' for incoming commands", _options.CommandsTopic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);

                    _logger.LogInformation(
                        "Command received from Kafka | Topic={Topic} | Partition={Partition} | Offset={Offset} | Key={Key}",
                        result.Topic, result.Partition, result.Offset, result.Message.Key);

                    await ProcessCommandAsync(result, stoppingToken);
                    consumer.Commit(result);

                    _logger.LogDebug("Command offset {Offset} committed successfully", result.Offset);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error in command consumer: {Reason}", ex.Error.Reason);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("KafkaCommandConsumer cancellation requested");
                    break;
                }
            }
        }
        finally
        {
            consumer.Close();
            _logger.LogInformation("KafkaCommandConsumer stopped");
        }
    }

    private async Task ProcessCommandAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        _logger.LogInformation("Processing command | Key={Key} | RawValue={RawValue}",
            result.Message.Key, result.Message.Value);

        var commandType = result.Message.Headers?
            .FirstOrDefault(h => h.Key == "command-type")?
            .GetValueBytes();

        var commandTypeName = commandType is not null
            ? System.Text.Encoding.UTF8.GetString(commandType)
            : "Unknown";

        _logger.LogInformation("Deserializing command of type: {CommandType}", commandTypeName);

        if (commandTypeName.Contains("UnpauseTransactionCommand"))
        {
            var command = JsonSerializer.Deserialize<UnpauseTransactionCommand>(result.Message.Value, DeserializeOpts);
            if (command is null)
            {
                _logger.LogError("Failed to deserialize UnpauseTransactionCommand from key {Key}", result.Message.Key);
                return;
            }

            _logger.LogInformation(
                "UnpauseTransactionCommand deserialized | TransactionId={TxId} | IssuedByUser={User} | CommandId={CmdId}",
                command.TransactionId, command.IssuedByUser, command.CommandId);

            await ProcessUnpauseCommandAsync(command, ct);
        }
        else
        {
            _logger.LogWarning("Unknown command type '{Type}' - no handler registered", commandTypeName);
        }
    }

    private async Task ProcessUnpauseCommandAsync(UnpauseTransactionCommand command, CancellationToken ct)
    {
        _logger.LogInformation(
            "Processing UnpauseTransactionCommand for transaction {TransactionId} by user {User}",
            command.TransactionId, command.IssuedByUser);

        var txRef = command.TransactionId;

        TransactionStatus resumeTo;
        using (var scope = _scopeFactory.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
            _logger.LogInformation("Fetching transaction {TxId} from database to determine resume state", txRef);
            var tx = await repo.GetDetailAsync(txRef, ct);

            if (tx is null)
            {
                _logger.LogWarning("Transaction {TxId} not found, cannot process unpause command", txRef);
                return;
            }

            _logger.LogInformation(
                "Transaction {TxId} found | CurrentStatus={Status} | IsPaused={IsPaused} | StatusHistoryCount={HistoryCount}",
                txRef, tx.CurrentStatus, tx.IsPaused, tx.StatusHistory?.Count ?? 0);

            var lastBeforePause = tx.StatusHistory?
                .Where(h => h.ToStatus == TransactionStatus.Paused)
                .MaxBy(h => h.OccurredAt)?.FromStatus;

            _logger.LogInformation("Last status before pause for {TxId}: {LastStatus}", txRef, lastBeforePause);

            resumeTo = lastBeforePause switch
            {
                TransactionStatus.CreditFailedRetry => TransactionStatus.CreditFailedRetry,
                _ => TransactionStatus.RegistrationFailedRetry,
            };

            _logger.LogInformation("Resume target determined for {TxId}: {ResumeTo}", txRef, resumeTo);
        }

        _logger.LogInformation("Step 1/3: Publishing TransactionStatusChanged: Paused -> {ResumeTo} for {TxId}", resumeTo, txRef);
        await PublishEventAsync(new TransactionStatusChanged
        {
            TransactionId = txRef,
            InternalRef = txRef,
            FromStatus = TransactionStatus.Paused,
            ToStatus = resumeTo,
            Reason = $"Unpaused by {command.IssuedByUser}; resuming from {resumeTo}",
            IsPaused = false,
            OccurredAt = DateTimeOffset.UtcNow,
        }, ct);

        _logger.LogInformation("Waiting 500ms before step 2/3 for {TxId}...", txRef);
        await Task.Delay(500, ct);

        var finalStatus = resumeTo == TransactionStatus.CreditFailedRetry
            ? TransactionStatus.CreditSucceeded
            : TransactionStatus.RegistrationSucceeded;
        var finalReason = resumeTo == TransactionStatus.CreditFailedRetry
            ? "Credit succeeded after manual unpause via Kafka"
            : "Partner registration succeeded after manual unpause via Kafka";

        _logger.LogInformation("Step 2/3: Publishing TransactionStatusChanged: {ResumeTo} -> {FinalStatus} for {TxId}",
            resumeTo, finalStatus, txRef);
        await PublishEventAsync(new TransactionStatusChanged
        {
            TransactionId = txRef,
            InternalRef = txRef,
            FromStatus = resumeTo,
            ToStatus = finalStatus,
            Reason = finalReason,
            IsPaused = false,
            OccurredAt = DateTimeOffset.UtcNow,
        }, ct);

        _logger.LogInformation("Step 3/3: Unpause command fully processed for transaction {TxId}", txRef);
    }

    private async Task PublishEventAsync(TransactionStatusChanged @event, CancellationToken ct)
    {
        var key = @event.TransactionId;
        var value = JsonSerializer.Serialize<TransferEvent>(@event, JsonOpts);

        _logger.LogInformation(
            "Publishing event to Kafka topic '{Topic}' | Key={Key} | EventType={Type} | Payload={Payload}",
            _options.EventsTopic, key, @event.GetType().Name, value);

        var result = await _producer.ProduceAsync(
            _options.EventsTopic,
            new Message<string, string>
            {
                Key = key,
                Value = value,
                Headers = new Headers
                {
                    new Header("event-type", System.Text.Encoding.UTF8.GetBytes(@event.GetType().Name)),
                    new Header("content-type", System.Text.Encoding.UTF8.GetBytes("application/json")),
                    new Header("source", System.Text.Encoding.UTF8.GetBytes("KafkaCommandConsumer")),
                    new Header("timestamp", System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O"))),
                },
            },
            ct);

        _logger.LogInformation(
            "Event published to Kafka | Topic={Topic} | Partition={Partition} | Offset={Offset} | Key={Key} | EventType={Type}",
            _options.EventsTopic, result.Partition, result.Offset, key, @event.GetType().Name);
    }

    public override void Dispose()
    {
        _logger.LogInformation("KafkaCommandConsumer disposing, flushing producer...");
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        base.Dispose();
        _logger.LogInformation("KafkaCommandConsumer disposed");
    }
}
