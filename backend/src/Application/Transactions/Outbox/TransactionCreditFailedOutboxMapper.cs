using System.Text.Json;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.Outbox.Entities;

namespace Universal.Transfers.Application.Transactions.Outbox;

public sealed class TransactionCreditFailedOutboxMapper : IDomainEventOutboxMapper<TransactionCreditFailedEvent>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public OutboxMessage Map(TransactionCreditFailedEvent @event) => new()
    {
        AggregateId = @event.InternalRef,
        EventType = @event.GetType().FullName!,
        Payload = JsonSerializer.Serialize(@event, SerializerOptions),
    };
}
