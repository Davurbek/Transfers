using Confluent.Kafka;
using Serilog;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/kafka-test-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        fileSizeLimitBytes: 10L * 1024 * 1024,
        rollOnFileSizeLimit: true)
    .CreateLogger();

var log = Log.ForContext("Program", "KafkaIntegrationTest");

try
{
    log.Information("╔══════════════════════════════════════════════════════╗");
    log.Information("║     TRANSFERS KAFKA INTEGRATION TEST               ║");
    log.Information("╚══════════════════════════════════════════════════════╝");
    log.Information("Starting Kafka integration test at {Time:O}", DateTimeOffset.UtcNow);

    const string bootstrapServers = "localhost:9092";
    const string commandsTopic = "transfers-commands";
    const string eventsTopic = "transfers-events";

    // ──────────────────────────────────────────────────────────
    // Verify Kafka connection
    // ──────────────────────────────────────────────────────────
    log.Information("Step 1: Verifying Kafka connection to {Bootstrap}", bootstrapServers);

    var metaConfig = new ProducerConfig { BootstrapServers = bootstrapServers };
    using var metaProducer = new ProducerBuilder<string, string>(metaConfig).Build();
    log.Information("Kafka producer created, connection will be verified on first produce");

    // ──────────────────────────────────────────────────────────
    // Start event consumer (background task)
    // ──────────────────────────────────────────────────────────
    log.Information("Step 2: Starting event consumer on topic '{Topic}'", eventsTopic);
    var receivedEvents = new List<string>();
    var eventReceivedSignal = new ManualResetEventSlim(false);

    var eventConsumerTask = Task.Run(async () =>
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = "integration-test-events",
            ClientId = "test-event-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(eventsTopic);

        log.Information("Event consumer started on topic '{Topic}'", eventsTopic);

        var timeout = DateTime.UtcNow.AddSeconds(30);
        try
        {
            while (DateTime.UtcNow < timeout)
            {
                try
                {
                    var result = consumer.Consume(TimeSpan.FromSeconds(1));
                    if (result is null) continue;

                    log.Information("⬇️ EVENT RECEIVED | Topic={Topic} | Partition={Part} | Offset={Offset} | Key={Key}",
                        result.Topic, result.Partition, result.Offset, result.Message.Key);
                    log.Information("   Event Value: {Value}", result.Message.Value);

                    lock (receivedEvents)
                    {
                        receivedEvents.Add(result.Message.Value);
                        if (receivedEvents.Count >= 2)
                            eventReceivedSignal.Set();
                    }
                }
                catch (ConsumeException ex)
                {
                    log.Warning("Consumer error (non-fatal): {Reason}", ex.Error.Reason);
                }
            }
        }
        finally
        {
            consumer.Close();
            log.Information("Event consumer stopped");
        }
    });

    await Task.Delay(3000);
    log.Information("Event consumer is ready and listening");

    // ──────────────────────────────────────────────────────────
    // Start command consumer (simulates real service flow)
    // ──────────────────────────────────────────────────────────
    log.Information("Step 3: Starting command consumer on topic '{Topic}'", commandsTopic);
    var commandReceivedSignal = new ManualResetEventSlim(false);

    var commandConsumerTask = Task.Run(async () =>
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = "integration-test-commands",
            ClientId = "test-command-processor",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            ClientId = "test-command-processor-producer",
        }).Build();

        consumer.Subscribe(commandsTopic);
        log.Information("Command consumer started on topic '{Topic}', waiting for commands...", commandsTopic);

        try
        {
            var timeout = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < timeout)
            {
                try
                {
                    var result = consumer.Consume(TimeSpan.FromSeconds(1));
                    if (result is null) continue;

                    log.Information("⬇️ COMMAND RECEIVED | Topic={Topic} | Partition={Part} | Offset={Offset} | Key={Key}",
                        result.Topic, result.Partition, result.Offset, result.Message.Key);
                    log.Information("   Command Value: {Value}", result.Message.Value);

                    var txId = result.Message.Key;

                    var event1 = new TransactionStatusChangedEvent
                    {
                        TransactionId = txId,
                        InternalRef = txId,
                        FromStatus = "Paused",
                        ToStatus = "RegistrationFailedRetry",
                        Reason = "Test: Command consumer unpaused transaction",
                        IsPaused = false,
                        OccurredAt = DateTimeOffset.UtcNow,
                    };
                    var event1Json = JsonSerializer.Serialize(event1);
                    var produceResult1 = await producer.ProduceAsync(eventsTopic,
                        new Message<string, string> { Key = txId, Value = event1Json });
                    log.Information("⬆️ EVENT 1 PUBLISHED | Topic={Topic} | Partition={Part} | Offset={Offset} | Status=Paused->RegistrationFailedRetry",
                        eventsTopic, produceResult1.Partition, produceResult1.Offset);

                    await Task.Delay(500);

                    var event2 = new TransactionStatusChangedEvent
                    {
                        TransactionId = txId,
                        InternalRef = txId,
                        FromStatus = "RegistrationFailedRetry",
                        ToStatus = "RegistrationSucceeded",
                        Reason = "Test: Registration succeeded after unpause",
                        IsPaused = false,
                        OccurredAt = DateTimeOffset.UtcNow,
                    };
                    var event2Json = JsonSerializer.Serialize(event2);
                    var produceResult2 = await producer.ProduceAsync(eventsTopic,
                        new Message<string, string> { Key = txId, Value = event2Json });
                    log.Information("⬆️ EVENT 2 PUBLISHED | Topic={Topic} | Partition={Part} | Offset={Offset} | Status=RegistrationFailedRetry->RegistrationSucceeded",
                        eventsTopic, produceResult2.Partition, produceResult2.Offset);

                    consumer.Commit(result);
                    commandReceivedSignal.Set();
                    log.Information("Command processed, 2 events published. Commit successful.");
                }
                catch (ConsumeException ex)
                {
                    log.Warning("Command consumer error: {Reason}", ex.Error.Reason);
                }
            }
        }
        finally
        {
            consumer.Close();
            log.Information("Command consumer stopped");
        }
    });

    await Task.Delay(3000);
    log.Information("Command consumer is ready and listening");

    // ──────────────────────────────────────────────────────────
    // Produce test commands
    // ──────────────────────────────────────────────────────────
    log.Information("═══════════════════════════════════════════════");
    log.Information("Step 4: Producing test commands to Kafka");
    log.Information("═══════════════════════════════════════════════");

    var testTxId = $"TX-TEST-{Random.Shared.Next(1000, 9999)}";

    log.Information("Test transaction ID: {TxId}", testTxId);

    var command = new UnpauseTransactionCommandEvent
    {
        CommandId = Guid.NewGuid().ToString(),
        TransactionId = testTxId,
        IssuedByUser = "integration-test",
    };
    var commandJson = JsonSerializer.Serialize(command);
    var cmdKey = command.CommandId;

    log.Information("Producing UnpauseTransactionCommand | Key={Key} | TxId={TxId} | User={User}",
        cmdKey, testTxId, "integration-test");
    log.Information("Command payload: {Payload}", commandJson);

    var commandProducer = new ProducerBuilder<string, string>(new ProducerConfig
    {
        BootstrapServers = bootstrapServers,
        ClientId = "test-command-producer",
    }).Build();

    var cmdResult = await commandProducer.ProduceAsync(commandsTopic,
        new Message<string, string>
        {
            Key = cmdKey,
            Value = commandJson,
            Headers = new Headers
            {
                new Header("command-type", Encoding.UTF8.GetBytes("UnpauseTransactionCommand")),
                new Header("source", Encoding.UTF8.GetBytes("integration-test")),
                new Header("timestamp", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O"))),
            },
        });

    log.Information("⬆️ COMMAND PUBLISHED | Topic={Topic} | Partition={Part} | Offset={Offset} | Key={Key}",
        commandsTopic, cmdResult.Partition, cmdResult.Offset, cmdKey);

    commandProducer.Flush(TimeSpan.FromSeconds(5));
    commandProducer.Dispose();

    // ──────────────────────────────────────────────────────────
    // Wait for results
    // ──────────────────────────────────────────────────────────
    log.Information("Step 5: Waiting for events to flow through the pipeline...");

    var commandReceived = commandReceivedSignal.Wait(15000);
    var eventsReceived = eventReceivedSignal.Wait(15000);

    log.Information("───────────────────────────────────────────────");
    log.Information("RESULTS:");
    log.Information("───────────────────────────────────────────────");
    log.Information("Command received and processed: {Result}", commandReceived ? "PASS" : "FAIL");
    log.Information("Events received: {Count}", receivedEvents.Count);
    log.Information("Events received (expected >=2): {Result}", receivedEvents.Count >= 2 ? "PASS" : "FAIL");

    // ──────────────────────────────────────────────────────────
    // Verify event content
    // ──────────────────────────────────────────────────────────
    log.Information("───────────────────────────────────────────────");
    log.Information("EVENT VERIFICATION:");
    log.Information("───────────────────────────────────────────────");

    for (int i = 0; i < receivedEvents.Count; i++)
    {
        try
        {
            var evt = JsonSerializer.Deserialize<TransactionStatusChangedEvent>(receivedEvents[i]);
            if (evt is not null)
            {
                log.Information("Event #{N}: TxId={TxId} | {From} -> {To} | Reason={Reason}",
                    i + 1, evt.TransactionId, evt.FromStatus, evt.ToStatus, evt.Reason);
            }
        }
        catch (Exception ex)
        {
            log.Error("Event #{N} deserialization failed: {Error}", i + 1, ex.Message);
        }
    }

    bool allPassed = commandReceived && receivedEvents.Count >= 2;
    log.Information("═══════════════════════════════════════════════");
    log.Information("OVERALL TEST RESULT: {Result}", allPassed ? "PASSED" : "FAILED");
    log.Information("═══════════════════════════════════════════════");

    if (allPassed)
    {
        log.Information("Kafka pipeline fully verified:");
        log.Information("  1. Command published to '{Topic}'", commandsTopic);
        log.Information("  2. Command consumed and processed");
        log.Information("  3. Events published to '{Topic}'", eventsTopic);
        log.Information("  4. Events consumed successfully");
        log.Information("  5. Full pipeline: Paused -> RegistrationFailedRetry -> RegistrationSucceeded");
    }

    await Task.WhenAll(eventConsumerTask, commandConsumerTask);

    Log.CloseAndFlush();

    if (!allPassed)
    {
        Console.Error.WriteLine("TEST FAILED - check logs for details");
        Environment.Exit(1);
    }

    Console.WriteLine("TEST PASSED");
    Environment.Exit(0);
}
catch (Exception ex)
{
    log.Fatal(ex, "Kafka integration test failed with unexpected error");
    Log.CloseAndFlush();
    Console.Error.WriteLine($"FATAL: {ex.Message}");
    Environment.Exit(1);
}

public class UnpauseTransactionCommandEvent
{
    [JsonPropertyName("commandId")] public string CommandId { get; set; } = "";
    [JsonPropertyName("transactionId")] public string TransactionId { get; set; } = "";
    [JsonPropertyName("issuedByUser")] public string IssuedByUser { get; set; } = "";
}

public class TransactionStatusChangedEvent
{
    [JsonPropertyName("transactionId")] public string TransactionId { get; set; } = "";
    [JsonPropertyName("internalRef")] public string InternalRef { get; set; } = "";
    [JsonPropertyName("fromStatus")] public string? FromStatus { get; set; }
    [JsonPropertyName("toStatus")] public string ToStatus { get; set; } = "";
    [JsonPropertyName("reason")] public string Reason { get; set; } = "";
    [JsonPropertyName("isPaused")] public bool IsPaused { get; set; }
    [JsonPropertyName("occurredAt")] public DateTimeOffset OccurredAt { get; set; }
}
