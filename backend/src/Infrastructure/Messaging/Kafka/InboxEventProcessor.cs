using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.Inbox.Entities;
using Universal.Transfers.Domain.Inbox.Interfaces;

namespace Universal.Transfers.Infrastructure.Messaging.Kafka;

public sealed class InboxEventProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InboxEventProcessor> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    public InboxEventProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<InboxEventProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InboxEventProcessor started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessNextBatchAsync(stoppingToken);
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing inbox events");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        finally
        {
            _logger.LogInformation("InboxEventProcessor stopped");
        }
    }

    private async Task ProcessNextBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var inboxRepo = scope.ServiceProvider.GetRequiredService<IInboxEventRepository>();
        var projector = scope.ServiceProvider.GetRequiredService<IEventProjector>();

        var events = await inboxRepo.GetUnprocessedEventsAsync(BatchSize, ct);
        if (events.Count == 0)
            return;

        _logger.LogInformation("Processing {Count} inbox events", events.Count);

        foreach (var inboxEvent in events)
        {
            try
            {
                var transferEvent = DeserializeEvent(inboxEvent);
                if (transferEvent is null)
                {
                    _logger.LogWarning("Could not deserialize inbox event {Id} type {Type}", inboxEvent.Id, inboxEvent.EventType);
                    await inboxRepo.MarkFailedAsync(inboxEvent.Id, "Deserialization failed", ct);
                    continue;
                }

                _logger.LogDebug("Projecting inbox event {Id} type {Type} occurred at {OccurredOn}",
                    inboxEvent.Id, inboxEvent.EventType, inboxEvent.OccurredOn);

                await projector.ProjectAsync(transferEvent, ct);
                await inboxRepo.MarkProcessedAsync(inboxEvent.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process inbox event {Id}", inboxEvent.Id);
                await inboxRepo.MarkFailedAsync(inboxEvent.Id, ex.Message, ct);
            }
        }

        await inboxRepo.SaveChangesAsync(ct);
    }

    private static TransferEvent? DeserializeEvent(InboxEvent inboxEvent)
    {
        return inboxEvent.EventType switch
        {
            nameof(TransactionInitiatedEvent) =>
                JsonSerializer.Deserialize<TransactionInitiatedEvent>(inboxEvent.Payload, JsonOpts),
            nameof(TransactionCreditCompletedEvent) =>
                JsonSerializer.Deserialize<TransactionCreditCompletedEvent>(inboxEvent.Payload, JsonOpts),
            nameof(TransactionCreditFailedEvent) =>
                JsonSerializer.Deserialize<TransactionCreditFailedEvent>(inboxEvent.Payload, JsonOpts),
            nameof(TransactionCreditFailedRetryEvent) =>
                JsonSerializer.Deserialize<TransactionCreditFailedRetryEvent>(inboxEvent.Payload, JsonOpts),
            nameof(TransactionCreditRetryRequestedEvent) =>
                JsonSerializer.Deserialize<TransactionCreditRetryRequestedEvent>(inboxEvent.Payload, JsonOpts),
            nameof(TransactionRegistrationCompletedEvent) =>
                JsonSerializer.Deserialize<TransactionRegistrationCompletedEvent>(inboxEvent.Payload, JsonOpts),
            nameof(TransactionRegistrationFailedRetryEvent) =>
                JsonSerializer.Deserialize<TransactionRegistrationFailedRetryEvent>(inboxEvent.Payload, JsonOpts),
            nameof(TransactionRegistrationRetryRequestedEvent) =>
                JsonSerializer.Deserialize<TransactionRegistrationRetryRequestedEvent>(inboxEvent.Payload, JsonOpts),
            nameof(TransactionPausedEvent) =>
                JsonSerializer.Deserialize<TransactionPausedEvent>(inboxEvent.Payload, JsonOpts),
            nameof(TransactionUnpausedEvent) =>
                JsonSerializer.Deserialize<TransactionUnpausedEvent>(inboxEvent.Payload, JsonOpts),
            _ => null,
        };
    }
}
