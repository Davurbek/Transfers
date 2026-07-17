using MassTransit;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Universal.Transfers.Application.Messaging;

namespace Universal.Transfers.Infrastructure.Messaging.MassTransit.Consumers;

public sealed class MainEventConsumer(
    IEventProjector projector,
    ILogger<MainEventConsumer> logger) : IConsumer<string>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task Consume(ConsumeContext<string> context)
    {
        var payload = context.Message;

        TransferEvent? @event;
        try
        {
            @event = JsonSerializer.Deserialize<TransferEvent>(payload, JsonOpts);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize MassTransit message: {Payload}", payload);
            return;
        }

        if (@event is null)
        {
            logger.LogWarning("Null event deserialized from MassTransit, raw: {Payload}", payload);
            return;
        }

        logger.LogInformation("MassTransit consumed event | Type={Type} | InternalRef={Ref}",
            @event.GetType().Name, GetInternalRef(@event));

        await projector.ProjectAsync(@event, context.CancellationToken);
    }

    private static string GetInternalRef(TransferEvent @event) => @event switch
    {
        TransactionInitiatedEvent e => e.InternalRef,
        TransactionCreditCompletedEvent e => e.InternalRef,
        TransactionCreditFailedEvent e => e.InternalRef,
        TransactionCreditFailedRetryEvent e => e.InternalRef,
        TransactionRegistrationCompletedEvent e => e.InternalRef,
        TransactionRegistrationFailedRetryEvent e => e.InternalRef,
        TransactionPausedEvent e => e.InternalRef,
        TransactionUnpausedEvent e => e.InternalRef,
        _ => "unknown",
    };
}
