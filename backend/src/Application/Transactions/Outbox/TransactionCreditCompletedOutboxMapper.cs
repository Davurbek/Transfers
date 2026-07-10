using System.Text.Json;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.Outbox.Entities;

namespace Universal.Transfers.Application.Transactions.Outbox;

public sealed class TransactionCreditCompletedOutboxMapper : IDomainEventOutboxMapper<TransactionCreditCompletedEvent>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public OutboxMessage Map(TransactionCreditCompletedEvent @event) => new()
    {
        AggregateId = @event.InternalRef,
        EventType = @event.GetType().FullName!,
        Payload = JsonSerializer.Serialize(@event, SerializerOptions),
    };
}
