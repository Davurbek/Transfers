using Microsoft.Extensions.Logging;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.Transactions.Entities;
using Universal.Transfers.Domain.Transactions.Interfaces;

namespace Universal.Transfers.Infrastructure.Transactions.Messaging;

public class EventProjector(
    ITransactionRepository txRepo,
    ILogger<EventProjector> logger) : IEventProjector
{
    public async Task ProjectAsync(TransferEvent @event, CancellationToken ct = default)
    {
        switch (@event)
        {
            case TransactionUpserted e:
                await ProjectUpsertedAsync(e, ct);
                break;
            case TransactionStatusChanged e:
                await ProjectStatusChangedAsync(e, ct);
                break;
            default:
                logger.LogWarning("Unknown event type: {Type}", @event.GetType().Name);
                break;
        }
    }

    private async Task ProjectUpsertedAsync(TransactionUpserted e, CancellationToken ct)
    {
        var existing = await txRepo.GetByTransactionIdAsync(e.TransactionId, ct);
        if (existing is not null) return;

        var tx = new Transaction
        {
            TransactionId = e.TransactionId,
            UserId = e.UserId,
            RecipientName = e.RecipientName,
            Amount = e.Amount,
            Currency = e.Currency,
            Corridor = e.Corridor,
            CurrentStatus = e.CurrentStatus,
            IsPaused = e.IsPaused,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
        };
        await txRepo.AddAsync(tx, ct);
        logger.LogInformation("Projected TransactionUpserted for {TxId}", e.TransactionId);
    }

    private async Task ProjectStatusChangedAsync(TransactionStatusChanged e, CancellationToken ct)
    {
        var tx = await txRepo.GetByTransactionIdAsync(e.TransactionId, ct);
        if (tx is null)
        {
            logger.LogWarning("Transaction {TxId} not found for status change", e.TransactionId);
            return;
        }

        if (await txRepo.StatusEventExistsAsync($"{e.TransactionId}-{e.ToStatus}", ct)) return;

        txRepo.AddStatusHistory(new TransactionStatusHistory
        {
            TransactionId = tx.Id,
            FromStatus = e.FromStatus,
            ToStatus = e.ToStatus,
            Reason = e.Reason,
            OccurredAt = DateTimeOffset.UtcNow,
            EventId = $"{e.TransactionId}-{e.ToStatus}",
        });
        logger.LogInformation("Projected TransactionStatusChanged for {TxId}: {From} -> {To}",
            e.TransactionId, e.FromStatus, e.ToStatus);
    }
}
