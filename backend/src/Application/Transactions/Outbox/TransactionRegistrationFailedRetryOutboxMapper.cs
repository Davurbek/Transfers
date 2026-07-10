using System.Text.Json;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.Outbox.Entities;

namespace Universal.Transfers.Application.Transactions.Outbox;

public sealed class TransactionRegistrationFailedRetryOutboxMapper : IDomainEventOutboxMapper<TransactionRegistrationFailedRetryEvent>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public OutboxMessage Map(TransactionRegistrationFailedRetryEvent @event)
    {
        var message = new OutboxMessage
        {
            AggregateId = @event.InternalRef,
            EventType = @event.GetType().FullName!,
            Payload = JsonSerializer.Serialize(@event, SerializerOptions),
        };

        if (@event.NextAttemptAt > DateTime.UtcNow)
            message.Defer(@event.NextAttemptAt);

        return message;
    }
}
