namespace Transfers.Dashboard.Business.Messaging;

/// <summary>
/// Publishes commands to the protected command queue (Dashboard -> Main App).
/// Replace the implementation with a Kafka/RabbitMQ producer in production.
/// </summary>
public interface ICommandPublisher
{
    Task PublishAsync(TransferCommand command, CancellationToken ct = default);
}

/// <summary>
/// Applies a consumed lifecycle event to the Dashboard DB. Implementations must
/// be idempotent (safe under broker re-delivery).
/// </summary>
public interface IEventProjector
{
    Task ProjectAsync(TransferEvent @event, CancellationToken ct = default);
}
