using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.Transactions.Entities;
using Universal.Transfers.Domain.Transactions.Enums;
using Universal.Transfers.Domain.Transactions.Interfaces;
using static Universal.Transfers.Domain.Transactions.Enums.TransactionStatus;

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
            CreditGateway = e.CreditGateway ?? CreditGateway.Humo,
            RemitterPartner = e.RemitterPartner ?? string.Empty,
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
        var maxRetries = 15;
        var retryDelay = TimeSpan.FromMilliseconds(500);

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await AttemptProjectStatusChangedAsync(e, ct);
                return;
            }
            catch (InvalidOperationException) when (attempt < maxRetries - 1)
            {
                logger.LogWarning("Transaction {TxId} not ready, retrying ({N}/{M})",
                    e.TransactionId, attempt + 1, maxRetries);
                await Task.Delay(retryDelay, ct);
            }
        }
    }

    private async Task AttemptProjectStatusChangedAsync(TransactionStatusChanged e, CancellationToken ct)
    {
        var tx = string.IsNullOrEmpty(e.InternalRef)
            ? await txRepo.GetByTransactionIdAsync(e.TransactionId, ct)
            : await txRepo.GetByInternalRefAsync(e.InternalRef, ct)
              ?? await txRepo.GetByTransactionIdAsync(e.TransactionId, ct);
        if (tx is null)
        {
            logger.LogWarning("Transaction {TxId} not found for status change, will retry", e.TransactionId);
            throw new InvalidOperationException($"Transaction {e.TransactionId} not found. Must process init before status changes.");
        }

        if (string.IsNullOrWhiteSpace(tx.InternalRef))
        {
            tx.InternalRef = ResolveInternalRef(e.TransactionId, e.InternalRef);
            logger.LogInformation("Backfilled InternalRef for {TxId}", e.TransactionId);
        }

        var internalRef = !string.IsNullOrWhiteSpace(tx.InternalRef) ? tx.InternalRef : e.TransactionId;
        var eventId = $"{internalRef}-{e.ToStatus}";
        var statusExists = await txRepo.StatusEventExistsAsync(eventId, ct);

        // Save child entities first (CreditAttempt, PartnerRegistration) so they persist
        // even if the status history or Transaction update races with another consumer.
        await SaveChildEntitiesAsync(tx, e, ct);

        if (!statusExists)
        {
            txRepo.AddStatusHistory(new TransactionStatusHistory
            {
                TransactionId = tx.Id,
                FromStatus = e.FromStatus ?? tx.CurrentStatus,
                ToStatus = e.ToStatus,
                Reason = e.Reason,
                OccurredAt = e.OccurredAt,
                EventId = eventId,
            });

            tx.CurrentStatus = e.ToStatus;
            tx.IsPaused = e.IsPaused;

            await txRepo.SaveChangesAsync(ct);

            logger.LogInformation("Projected TransactionStatusChanged for {TxId}: {From} -> {To}",
                e.TransactionId, e.FromStatus, e.ToStatus);
        }
        else
        {
            tx.CurrentStatus = e.ToStatus;
            tx.IsPaused = e.IsPaused;

            await txRepo.SaveChangesAsync(ct);
        }
    }

    private async Task SaveChildEntitiesAsync(Transaction tx, TransactionStatusChanged e, CancellationToken ct)
    {
        if (e.ToStatus is CreditSucceeded or CreditFailed or CreditFailedRetry && e.AttemptNumber.HasValue)
        {
            var attemptEventId = $"{tx.InternalRef}-ca-{e.AttemptNumber}";
            if (!await txRepo.CreditAttemptExistsAsync(attemptEventId, ct))
            {
                tx.CreditAttempts.Add(new CreditAttempt
                {
                    TransactionId = tx.Id,
                    AttemptNumber = e.AttemptNumber.Value,
                    Gateway = tx.CreditGateway,
                    Status = e.ToStatus == CreditSucceeded ? OperationResult.Succeeded : OperationResult.Failed,
                    FailureCode = e.FailureReason,
                    AttemptedAt = e.OccurredAt,
                    EventId = attemptEventId,
                });
                await TrySaveChangesAsync(ct);
                logger.LogInformation("Projected CreditAttempt {N} for {Ref}: {Gw} -> {Status}",
                    e.AttemptNumber, tx.InternalRef, tx.CreditGateway,
                    e.ToStatus == CreditSucceeded ? "Succeeded" : "Failed");
            }
        }

        if (e.ToStatus is RegistrationSucceeded or RegistrationFailedRetry && e.AttemptNumber.HasValue)
        {
            var partnerName = !string.IsNullOrWhiteSpace(e.PartnerName) ? e.PartnerName : tx.RemitterPartner;
            var regEventId = $"{tx.InternalRef}-pr-{e.AttemptNumber}";
            if (!await txRepo.PartnerRegistrationExistsAsync(regEventId, ct))
            {
                tx.PartnerRegistrations.Add(new PartnerRegistration
                {
                    TransactionId = tx.Id,
                    PartnerName = partnerName,
                    Status = e.ToStatus == RegistrationSucceeded ? OperationResult.Succeeded : OperationResult.Failed,
                    FailureReason = e.FailureReason,
                    RegisteredAt = e.OccurredAt,
                    EventId = regEventId,
                });
                await TrySaveChangesAsync(ct);
                logger.LogInformation("Projected PartnerRegistration for {Ref}: {Partner} -> {Status}",
                    tx.InternalRef, partnerName,
                    e.ToStatus == RegistrationSucceeded ? "Succeeded" : "Failed");
            }
        }
    }

    private async Task TrySaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await txRepo.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            txRepo.DetachAddedEntities();
        }
    }

}
