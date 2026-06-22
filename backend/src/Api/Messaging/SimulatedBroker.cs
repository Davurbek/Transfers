using Microsoft.Extensions.Configuration;
using System.Threading.Channels;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.Transactions.Enums;

namespace Universal.Transfers.Api.Messaging;

public sealed class SimulatedBroker(
    IServiceProvider services,
    ILogger<SimulatedBroker> logger,
    IConfiguration configuration) : BackgroundService, ICommandPublisher
{
    private readonly Channel<TransferCommand> _commands =
        Channel.CreateUnbounded<TransferCommand>(new UnboundedChannelOptions { SingleReader = true });
    private readonly int _delayMs = configuration.GetValue<int>("SimulatedBroker:DelayMilliseconds", 1500);

    public async Task PublishAsync(TransferCommand command, CancellationToken ct = default)
    {
        logger.LogInformation("Command published: {Type} ({CommandId}) by {User}",
            command.GetType().Name, command.CommandId, command.IssuedByUser);
        await _commands.Writer.WriteAsync(command, ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
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
        catch (OperationCanceledException)
        {
            logger.LogInformation("SimulatedBroker is shutting down");
        }
    }

    private async Task HandleCommandAsync(TransferCommand command, CancellationToken ct)
    {
        await Task.Delay(_delayMs, ct);

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

                await Task.Delay(_delayMs, ct);

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

    private async Task EmitAsync(TransferEvent @event, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var projector = scope.ServiceProvider.GetRequiredService<IEventProjector>();
        await projector.ProjectAsync(@event, ct);
    }
}
