using System.Threading.Channels;
using Transfers.Dashboard.Business.Messaging;
using Transfers.Dashboard.Domain.Entities.Transactions;

namespace Transfers.Dashboard.Api.Messaging;

/// <summary>
/// In-process stand-in for Kafka/RabbitMQ used by the PoC. It:
///   1. accepts commands from the BLL (ICommandPublisher),
///   2. mimics the Main App re-running its state machine, and
///   3. emits the resulting lifecycle event back, projected into the Dashboard DB.
///
/// In production this whole class is replaced by real broker producer/consumer
/// clients; the controllers, services and repositories are unchanged.
/// </summary>
public sealed class SimulatedBroker(IServiceProvider services, ILogger<SimulatedBroker> logger)
    : BackgroundService, ICommandPublisher
{
    private readonly Channel<TransferCommand> _commands =
        Channel.CreateUnbounded<TransferCommand>(new UnboundedChannelOptions { SingleReader = true });

    public async Task PublishAsync(TransferCommand command, CancellationToken ct = default)
    {
        logger.LogInformation("Command published: {Type} ({CommandId}) by {User}",
            command.GetType().Name, command.CommandId, command.IssuedByUser);
        await _commands.Writer.WriteAsync(command, ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var command in _commands.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await HandleCommandAsync(command, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process simulated command {CommandId}", command.CommandId);
            }
        }
    }

    private async Task HandleCommandAsync(TransferCommand command, CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(1500), ct);

        switch (command)
        {
            case UnpauseTransactionCommand unpause:
                await EmitAsync(new TransactionStatusChanged
                {
                    TransactionId = unpause.TransactionId,
                    FromStatus = TransactionStatus.Paused,
                    ToStatus = TransactionStatus.RegistrationFailedRetry,
                    Reason = $"Unpaused by {unpause.IssuedByUser}; retrying partner registration",
                    IsPaused = false,
                }, ct);

                await Task.Delay(TimeSpan.FromMilliseconds(1500), ct);

                await EmitAsync(new TransactionStatusChanged
                {
                    TransactionId = unpause.TransactionId,
                    FromStatus = TransactionStatus.RegistrationFailedRetry,
                    ToStatus = TransactionStatus.RegistrationSucceeded,
                    Reason = "Partner registration succeeded after manual unpause",
                    IsPaused = false,
                }, ct);
                break;

            default:
                logger.LogWarning("No simulated handler for {Type}", command.GetType().Name);
                break;
        }
    }

    /// <summary>Mimics the broker delivering an event back to the dashboard consumer.</summary>
    private async Task EmitAsync(TransferEvent @event, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var projector = scope.ServiceProvider.GetRequiredService<IEventProjector>();
        await projector.ProjectAsync(@event, ct);
    }
}
