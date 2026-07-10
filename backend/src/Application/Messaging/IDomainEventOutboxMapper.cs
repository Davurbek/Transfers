using Universal.Transfers.Domain.Outbox.Entities;

namespace Universal.Transfers.Application.Messaging;

public interface IDomainEventOutboxMapper<TEvent> where TEvent : TransferEvent
{
    OutboxMessage Map(TEvent @event);
}
