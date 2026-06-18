using Microsoft.Extensions.Logging;
using Transfers.Dashboard.DataAccess.Repositories;
using Transfers.Dashboard.Domain.Entities.Transactions;

namespace Transfers.Dashboard.Business.Messaging;

/// <summary>
/// Idempotently projects consumed lifecycle events into the read-optimized
/// Dashboard DB (the "read path" sink). Works through the repository layer only.
/// </summary>
public sealed class EventProjector(
    ITransactionRepository transactions,
    IUnitOfWork unitOfWork,
    ILogger<EventProjector> logger) : IEventProjector
{
    public async Task ProjectAsync(TransferEvent @event, CancellationToken ct = default)
    {
        switch (@event)
        {
            case TransactionUpserted up:
                await ApplyUpsertAsync(up, ct);
                break;
            case TransactionStatusChanged ch:
                await ApplyStatusChangeAsync(ch, ct);
                break;
            default:
                logger.LogWarning("Unhandled event type {Type}", @event.GetType().Name);
                break;
        }
    }

    private async Task ApplyUpsertAsync(TransactionUpserted e, CancellationToken ct)
    {
        var tx = await transactions.GetByTransactionIdAsync(e.TransactionId, ct);
        if (tx is null)
        {
            tx = new Transaction { TransactionId = e.TransactionId, CreatedAt = e.OccurredAt };
            await transactions.AddAsync(tx, ct);
        }

        tx.UserId = e.UserId;
        tx.RecipientName = e.RecipientName;
        tx.Amount = e.Amount;
        tx.Currency = e.Currency;
        tx.Corridor = e.Corridor;
        tx.CurrentStatus = e.Status;
        tx.IsPaused = e.IsPaused;
        tx.UpdatedAt = e.OccurredAt;

        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task ApplyStatusChangeAsync(TransactionStatusChanged e, CancellationToken ct)
    {
        // Idempotency: skip if we've already recorded this event id.
        if (await transactions.StatusEventExistsAsync(e.EventId, ct))
        {
            logger.LogDebug("Duplicate event {EventId} ignored", e.EventId);
            return;
        }

        var tx = await transactions.GetByTransactionIdAsync(e.TransactionId, ct);
        if (tx is null)
        {
            logger.LogWarning("Status change for unknown tx {TxId}", e.TransactionId);
            return;
        }

        transactions.AddStatusHistory(new TransactionStatusHistory
        {
            TransactionId = tx.Id,
            FromStatus = e.FromStatus ?? tx.CurrentStatus,
            ToStatus = e.ToStatus,
            Reason = e.Reason,
            OccurredAt = e.OccurredAt,
            EventId = e.EventId,
        });

        tx.CurrentStatus = e.ToStatus;
        tx.IsPaused = e.IsPaused;
        tx.UpdatedAt = e.OccurredAt;

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Projected status change {TxId} -> {Status}", e.TransactionId, e.ToStatus);
    }
}
