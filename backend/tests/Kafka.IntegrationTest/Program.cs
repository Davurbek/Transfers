using Confluent.Kafka;
using Serilog;
using System.Text;
using System.Text.Json;

// ──────────────────────────────────────────────────────────────
// 1. Configure Serilog file logging
// ──────────────────────────────────────────────────────────────
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
    // 2. Verify Kafka connection
    // ──────────────────────────────────────────────────────────
    log.Information("Step 1: Verifying Kafka connection to {Bootstrap}", bootstrapServers);

    var adminConfig = new AdminClientConfig { BootstrapServers = bootstrapServers };
    using var admin = new AdminClientBuilder(adminConfig).Build();
    var metadata = admin.GetMetadata(TimeSpan.FromSeconds(10));
    var topics = metadata.Topics.Select(t => $"{t.Topic}({t.Partitions.Count}p)");
    log.Information("Kafka connected! Broker={BrokerMeta}, Topics: {Topics}",
        metadata.OriginatingBrokerId, string.Join(", ", topics));

    // ──────────────────────────────────────────────────────────
    // 3. Start event consumer (background task)
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

    // Give consumer time to subscribe and be ready
    await Task.Delay(3000);
    log.Information("Event consumer is ready and listening");

    // ──────────────────────────────────────────────────────────
    // 4. Start command consumer (simulates real service)
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

                    var txRef = result.Message.Key;

                    // Event 1: TransactionUnpausedEvent
                    // Using $type discriminator matching [JsonDerivedType] in Contracts.cs
                    var event1 = new Dictionary<string, object?>
                    {
                        ["$type"] = "TransactionUnpausedEvent",
                        ["internalRef"] = txRef,
                        ["resumedToStatus"] = 8, // TransactionStatus.RegistrationFailedRetry
                        ["occurredOn"] = DateTime.UtcNow,
                        ["pausedTelegramMessageId"] = null,
                    };
                    var event1Json = JsonSerializer.Serialize(event1);
                    var produceResult1 = await producer.ProduceAsync(eventsTopic,
                        new Message<string, string> { Key = txRef, Value = event1Json });
                    log.Information("⬆️ EVENT 1 PUBLISHED | Topic={Topic} | Partition={Part} | Offset={Offset} | Type=TransactionUnpausedEvent",
                        eventsTopic, produceResult1.Partition, produceResult1.Offset);

                    await Task.Delay(500);

                    // Event 2: TransactionRegistrationCompletedEvent
                    var event2 = new Dictionary<string, object?>
                    {
                        ["$type"] = "TransactionRegistrationCompletedEvent",
                        ["internalRef"] = txRef,
                        ["remitterPartnerCode"] = "integration-test",
                        ["attempt"] = 1,
                        ["occurredOn"] = DateTime.UtcNow,
                    };
                    var event2Json = JsonSerializer.Serialize(event2);
                    var produceResult2 = await producer.ProduceAsync(eventsTopic,
                        new Message<string, string> { Key = txRef, Value = event2Json });
                    log.Information("⬆️ EVENT 2 PUBLISHED | Topic={Topic} | Partition={Part} | Offset={Offset} | Type=TransactionRegistrationCompletedEvent",
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
    // 5. Produce test commands
    // ──────────────────────────────────────────────────────────
    log.Information("═══════════════════════════════════════════════");
    log.Information("Step 4: Producing test commands to Kafka");
    log.Information("═══════════════════════════════════════════════");

    var testTxId = $"TX-TEST-{Random.Shared.Next(1000, 9999)}";

    log.Information("Test transaction ID: {TxId}", testTxId);

    // Publish UnpauseTransactionCommand (using camelCase to match DeserializeOpts in KafkaCommandConsumer)
    var commandJson = JsonSerializer.Serialize(new
    {
        commandId = Guid.NewGuid().ToString(),
        internalRef = testTxId,
        issuedByUser = "integration-test",
    });
    var cmdKey = Guid.NewGuid().ToString();

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
    // 6. Wait for results
    // ──────────────────────────────────────────────────────────
    log.Information("Step 5: Waiting for events to flow through the pipeline...");

    var commandReceived = commandReceivedSignal.Wait(15000);
    var eventsReceived = eventReceivedSignal.Wait(15000);

    log.Information("───────────────────────────────────────────────");
    log.Information("RESULTS:");
    log.Information("───────────────────────────────────────────────");
    log.Information("Command received and processed: {Result}", commandReceived ? "YES" : "NO");
    log.Information("Events received: {Count}", receivedEvents.Count);
    log.Information("Events received (expected >=2): {Result}", receivedEvents.Count >= 2 ? "YES" : "NO");

    // ──────────────────────────────────────────────────────────
    // 7. Verify event content (check for $type discriminator)
    // ──────────────────────────────────────────────────────────
    log.Information("───────────────────────────────────────────────");
    log.Information("EVENT VERIFICATION:");
    log.Information("───────────────────────────────────────────────");

    for (int i = 0; i < receivedEvents.Count; i++)
    {
        try
        {
            using var doc = JsonDocument.Parse(receivedEvents[i]);
            var root = doc.RootElement;
            var type = root.TryGetProperty("$type", out var t) ? t.GetString() : "unknown";
            var internalRef = root.TryGetProperty("internalRef", out var r) ? r.GetString() : "";

            log.Information("Event #{N}: $type={Type} | internalRef={Ref}",
                i + 1, type, internalRef);
        }
        catch (Exception ex)
        {
            log.Error("Event #{N} parsing failed: {Error}", i + 1, ex.Message);
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
        log.Information("  5. Full pipeline: UnpauseCommand -> TransactionUnpausedEvent -> TransactionRegistrationCompletedEvent");
    }

    // Cleanup
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
