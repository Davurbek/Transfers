using Universal.Transfers.Application.Messaging;

namespace Universal.Transfers.Infrastructure.Messaging.MassTransit;

public static class EventRouter
{
    public static TransferEvent? Route(object @event) => @event switch
    {
        TransactionInitiatedEvent e => e,
        TransactionCreditCompletedEvent e => e,
        TransactionCreditFailedEvent e => e,
        TransactionCreditFailedRetryEvent e => e,
        TransactionRegistrationCompletedEvent e => e,
        TransactionRegistrationFailedRetryEvent e => e,
        TransactionPausedEvent e => e,
        TransactionUnpausedEvent e => e,
        _ => null,
    };
}
