using Microsoft.EntityFrameworkCore;
using Transfers.Dashboard.Api.Data;
using Transfers.Dashboard.Api.Domain.Transactions;

namespace Transfers.Dashboard.Api.Messaging;

/// <summary>
/// Idempotently projects consumed lifecycle events into the read-optimized
/// Dashboard DB. This is the "read path" sink described in the architecture doc.
/// </summary>
public class EventProjector(DashboardDbContext db, ILogger<EventProjector> logger) : IEventProjector
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
        var tx = await db.Transactions.FirstOrDefaultAsync(t => t.TransactionId == e.TransactionId, ct);
        if (tx is null)
        {
            tx = new Transaction { TransactionId = e.TransactionId, CreatedAt = e.OccurredAt };
            db.Transactions.Add(tx);
        }

        tx.UserId = e.UserId;
        tx.RecipientName = e.RecipientName;
        tx.Amount = e.Amount;
        tx.Currency = e.Currency;
        tx.Corridor = e.Corridor;
        tx.CurrentStatus = e.Status;
        tx.IsPaused = e.IsPaused;
        tx.UpdatedAt = e.OccurredAt;

        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyStatusChangeAsync(TransactionStatusChanged e, CancellationToken ct)
    {
        // Idempotency: skip if we've already recorded this event id.
        if (await db.TransactionStatusHistory.AnyAsync(h => h.EventId == e.EventId, ct))
        {
            logger.LogDebug("Duplicate event {EventId} ignored", e.EventId);
            return;
        }

        var tx = await db.Transactions.FirstOrDefaultAsync(t => t.TransactionId == e.TransactionId, ct);
        if (tx is null)
        {
            logger.LogWarning("Status change for unknown tx {TxId}", e.TransactionId);
            return;
        }

        db.TransactionStatusHistory.Add(new TransactionStatusHistory
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

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Projected status change {TxId} -> {Status}", e.TransactionId, e.ToStatus);
    }
}
