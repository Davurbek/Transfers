namespace Universal.Transfers.Application.Messaging;

public interface IEventConsumer
{
    Task ConsumeAsync(TransferEvent @event, CancellationToken ct = default);
}
