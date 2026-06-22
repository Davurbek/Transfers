namespace Universal.Transfers.Application.Messaging;

public interface IEventProjector
{
    Task ProjectAsync(TransferEvent @event, CancellationToken ct = default);
}
