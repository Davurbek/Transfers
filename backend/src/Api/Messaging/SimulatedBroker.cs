using Microsoft.Extensions.Configuration;
using System.Threading.Channels;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.Transactions.Enums;
using Universal.Transfers.Domain.Transactions.Interfaces;

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
                var txRef = unpause.TransactionId;

                TransactionStatus resumeTo;
                using (var scope = services.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
                    var tx = await repo.GetDetailAsync(txRef, ct);
                    var lastBeforePause = tx?.StatusHistory
                        .Where(h => h.ToStatus == TransactionStatus.Paused)
                        .MaxBy(h => h.OccurredAt)?.FromStatus;
                    resumeTo = lastBeforePause switch
                    {
                        TransactionStatus.CreditFailedRetry => TransactionStatus.CreditFailedRetry,
                        _ => TransactionStatus.RegistrationFailedRetry,
                    };
                }

                await EmitAsync(new TransactionStatusChanged
                {
                    TransactionId = txRef,
                    FromStatus = TransactionStatus.Paused,
                    ToStatus = resumeTo,
                    Reason = $"Unpaused by {unpause.IssuedByUser}; resuming from {resumeTo}",
                    IsPaused = false,
                }, ct);

                await Task.Delay(_delayMs, ct);

                var finalStatus = resumeTo == TransactionStatus.CreditFailedRetry
                    ? TransactionStatus.CreditSucceeded
                    : TransactionStatus.RegistrationSucceeded;
                var finalReason = resumeTo == TransactionStatus.CreditFailedRetry
                    ? "Credit succeeded after manual unpause"
                    : "Partner registration succeeded after manual unpause";

                await EmitAsync(new TransactionStatusChanged
                {
                    TransactionId = txRef,
                    FromStatus = resumeTo,
                    ToStatus = finalStatus,
                    Reason = finalReason,
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
