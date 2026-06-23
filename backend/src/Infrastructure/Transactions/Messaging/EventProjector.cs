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

    private static string ResolveInternalRef(string transactionId, string? internalRef) =>
        !string.IsNullOrWhiteSpace(internalRef) ? internalRef : $"intref-{transactionId}";

    private async Task ProjectUpsertedAsync(TransactionUpserted e, CancellationToken ct)
    {
        var existing = await txRepo.GetByTransactionIdAsync(e.TransactionId, ct);
        if (existing is not null)
        {
            if (string.IsNullOrWhiteSpace(existing.InternalRef) && !string.IsNullOrWhiteSpace(e.InternalRef))
            {
                existing.InternalRef = e.InternalRef;
                await txRepo.SaveChangesAsync(ct);
                logger.LogInformation("Backfilled InternalRef for {TxId}", e.TransactionId);
            }
            return;
        }

        var tx = new Transaction
        {
            InternalRef = ResolveInternalRef(e.TransactionId, e.InternalRef),
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

        var internalRef = tx.InternalRef;
        var statusEventId = $"{internalRef}-{e.CurrentStatus}";
        if (!await txRepo.StatusEventExistsAsync(statusEventId, ct))
        {
            txRepo.AddStatusHistory(new TransactionStatusHistory
            {
                TransactionId = tx.Id,
                FromStatus = null,
                ToStatus = e.CurrentStatus,
                Reason = "Transaction initiated",
                OccurredAt = e.UpdatedAt,
                EventId = statusEventId,
            });
        }

        await txRepo.AddAsync(tx, ct);
        await txRepo.SaveChangesAsync(ct);
        logger.LogInformation("Projected TransactionUpserted for {TxId}", e.TransactionId);
    }

    private async Task ProjectStatusChangedAsync(TransactionStatusChanged e, CancellationToken ct)
    {
        var tx = string.IsNullOrEmpty(e.InternalRef)
            ? await txRepo.GetByTransactionIdAsync(e.TransactionId, ct)
            : await txRepo.GetByInternalRefAsync(e.InternalRef, ct)
              ?? await txRepo.GetByTransactionIdAsync(e.TransactionId, ct);
        if (tx is null)
        {
            logger.LogWarning("Transaction {TxId} not found for status change", e.TransactionId);
            return;
        }

        if (string.IsNullOrWhiteSpace(tx.InternalRef))
        {
            tx.InternalRef = ResolveInternalRef(e.TransactionId, e.InternalRef);
            logger.LogInformation("Backfilled InternalRef for {TxId}", e.TransactionId);
        }

        var internalRef = !string.IsNullOrWhiteSpace(tx.InternalRef) ? tx.InternalRef : e.TransactionId;
        var eventId = $"{internalRef}-{e.ToStatus}";
        if (await txRepo.StatusEventExistsAsync(eventId, ct)) return;

        txRepo.AddStatusHistory(new TransactionStatusHistory
        {
            TransactionId = tx.Id,
            FromStatus = e.FromStatus ?? tx.CurrentStatus,
            ToStatus = e.ToStatus,
            Reason = e.Reason,
            OccurredAt = e.OccurredAt,
            EventId = eventId,
        });

        await txRepo.UpdateCurrentStatusAsync(tx.Id, e.ToStatus, e.IsPaused, ct);
        await txRepo.SaveChangesAsync(ct);

        logger.LogInformation("Projected TransactionStatusChanged for {TxId}: {From} -> {To}",
            e.TransactionId, e.FromStatus, e.ToStatus);
    }
}
