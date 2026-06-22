namespace Universal.Transfers.Application.Messaging;

public interface ICommandPublisher
{
    Task PublishAsync(TransferCommand command, CancellationToken ct = default);
}
