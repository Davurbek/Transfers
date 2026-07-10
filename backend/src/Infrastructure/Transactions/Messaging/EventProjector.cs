using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.Transactions.Entities;
using Universal.Transfers.Domain.Transactions.Enums;
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
            case TransactionInitiatedEvent e:
                await ProjectInitiatedAsync(e, ct);
                break;
            case TransactionCreditCompletedEvent e:
                await ProjectCreditCompletedAsync(e, ct);
                break;
            case TransactionCreditFailedEvent e:
                await ProjectCreditFailedAsync(e, ct);
                break;
            case TransactionCreditFailedRetryEvent e:
                await ProjectCreditFailedRetryAsync(e, ct);
                break;
            case TransactionCreditRetryRequestedEvent e:
                await ProjectCreditRetryRequestedAsync(e, ct);
                break;
            case TransactionRegistrationCompletedEvent e:
                await ProjectRegistrationCompletedAsync(e, ct);
                break;
            case TransactionRegistrationFailedRetryEvent e:
                await ProjectRegistrationFailedRetryAsync(e, ct);
                break;
            case TransactionRegistrationRetryRequestedEvent e:
                await ProjectRegistrationRetryRequestedAsync(e, ct);
                break;
            case TransactionPausedEvent e:
                await ProjectPausedAsync(e, ct);
                break;
            case TransactionUnpausedEvent e:
                await ProjectUnpausedAsync(e, ct);
                break;
            default:
                logger.LogWarning("Unknown event type: {Type}", @event.GetType().Name);
                break;
        }
    }

    private async Task<Transaction> GetOrCreateTransaction(string internalRef, CancellationToken ct)
    {
        var tx = await txRepo.GetByInternalRefAsync(internalRef, ct);
        if (tx is not null) return tx;

        tx = new Transaction
        {
            InternalRef = internalRef,
            TransactionId = internalRef,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await txRepo.AddAsync(tx, ct);
        await txRepo.SaveChangesAsync(ct);
        logger.LogInformation("Created new transaction from event: {InternalRef}", internalRef);
        return tx;
    }

    private async Task AppendStatusHistory(Transaction tx, TransactionStatus? from, TransactionStatus to, string? reason, string eventIdSuffix, DateTime occurredOn, CancellationToken ct)
    {
        var eventId = $"{tx.InternalRef}-{eventIdSuffix}";
        if (await txRepo.StatusEventExistsAsync(eventId, ct))
            return;

        txRepo.AddStatusHistory(new TransactionStatusHistory
        {
            TransactionId = tx.Id,
            FromStatus = from ?? tx.CurrentStatus,
            ToStatus = to,
            Reason = reason,
            OccurredAt = new DateTimeOffset(occurredOn, TimeSpan.Zero),
            EventId = eventId,
        });
        tx.CurrentStatus = to;
        tx.UpdatedAt = new DateTimeOffset(occurredOn, TimeSpan.Zero);
        await txRepo.SaveChangesAsync(ct);
    }

    private async Task ProjectInitiatedAsync(TransactionInitiatedEvent e, CancellationToken ct)
    {
        var tx = await txRepo.GetByInternalRefAsync(e.InternalRef, ct);
        if (tx is not null)
        {
            logger.LogInformation("Transaction {InternalRef} already exists, skipping init", e.InternalRef);
            return;
        }

        var (amount, currency) = ResolveAmountAndCurrency(e);

        tx = new Transaction
        {
            InternalRef = e.InternalRef,
            PartnerRef = e.PartnerRef,
            TransactionId = e.InternalRef,
            UserId = string.Empty,
            RecipientName = e.ReceiverCardLast4,
            Amount = amount,
            Currency = currency,
            Corridor = e.TransactionType.ToString(),
            CurrentStatus = TransactionStatus.ConfirmSucceeded,
            IsPaused = false,
            CreatedAt = new DateTimeOffset(e.OccurredOn, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(e.OccurredOn, TimeSpan.Zero),
            RemitterPartner = e.RemitterPartnerCode,
            PaymentPartner = e.PaymentPartner.ToString(),
        };
        await txRepo.AddAsync(tx, ct);

        AppendInitStatusHistory(tx, e.OccurredOn);
        await txRepo.SaveChangesAsync(ct);
        logger.LogInformation("Projected TransactionInitiatedEvent for {InternalRef} | Amount={Amount} {Currency}", e.InternalRef, amount, currency);
    }

    private static (decimal amount, string currency) ResolveAmountAndCurrency(TransactionInitiatedEvent e)
    {
        var amount = e.CreditAmount;
        var currency = e.CreditAmountCurrency ?? string.Empty;

        if (amount != 0m && !string.IsNullOrWhiteSpace(currency))
            return (amount, currency);

        if (e.ExtensionData is null)
            return (amount, currency);

        if (amount == 0m)
        {
            if (TryGetDecimal(e.ExtensionData, "amount", out var v)) amount = v;
            else if (TryGetDecimal(e.ExtensionData, "credit_amount", out v)) amount = v;
            else if (TryGetDecimal(e.ExtensionData, "sum", out v)) amount = v;
            else if (TryGetDecimal(e.ExtensionData, "creditAmount", out v)) amount = v;
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            if (TryGetString(e.ExtensionData, "currency", out var c)) currency = c!;
            else if (TryGetString(e.ExtensionData, "creditAmountCurrency", out c)) currency = c!;
            else if (TryGetString(e.ExtensionData, "credit_amount_currency", out c)) currency = c!;
            else if (TryGetString(e.ExtensionData, "currencyCode", out c)) currency = c!;
        }

        return (amount, currency);
    }

    private static bool TryGetDecimal(Dictionary<string, JsonElement> ext, string key, out decimal value)
    {
        value = 0m;
        if (!ext.TryGetValue(key, out var el) || el.ValueKind == JsonValueKind.Null)
            return false;
        try
        {
            value = el.ValueKind == JsonValueKind.String
                ? decimal.Parse(el.GetString()!)
                : el.GetDecimal();
            return true;
        }
        catch { return false; }
    }

    private static bool TryGetString(Dictionary<string, JsonElement> ext, string key, out string? value)
    {
        value = null;
        if (!ext.TryGetValue(key, out var el) || el.ValueKind == JsonValueKind.Null)
            return false;
        try
        {
            value = el.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }
        catch { return false; }
    }

    private static void AppendInitStatusHistory(Transaction tx, DateTime occurredOn)
    {
        tx.StatusHistory.Add(new TransactionStatusHistory
        {
            TransactionId = tx.Id,
            FromStatus = null,
            ToStatus = TransactionStatus.ConfirmSucceeded,
            Reason = "Transaction initiated",
            OccurredAt = new DateTimeOffset(occurredOn, TimeSpan.Zero),
            EventId = $"{tx.InternalRef}-init",
        });
    }

    private async Task ProjectCreditCompletedAsync(TransactionCreditCompletedEvent e, CancellationToken ct)
    {
        var tx = await GetOrCreateTransaction(e.InternalRef, ct);
        if (await txRepo.CreditAttemptExistsAsync($"{e.InternalRef}-ca-{e.Attempt}", ct))
        {
            await AppendStatusHistory(tx, TransactionStatus.CreditSucceeded, TransactionStatus.CreditSucceeded,
                $"Credit completed (attempt {e.Attempt})", $"credit-completed-{e.Attempt}", e.OccurredOn, ct);
            return;
        }

        txRepo.AddCreditAttempt(new CreditAttempt
        {
            TransactionId = tx.Id,
            AttemptNumber = e.Attempt,
            Gateway = CreditGateway.Humo,
            Status = OperationResult.Succeeded,
            AttemptedAt = new DateTimeOffset(e.OccurredOn, TimeSpan.Zero),
            EventId = $"{e.InternalRef}-ca-{e.Attempt}",
        });

        await AppendStatusHistory(tx, TransactionStatus.CreditSucceeded, TransactionStatus.CreditSucceeded,
            $"Credit succeeded (attempt {e.Attempt})", $"credit-completed-{e.Attempt}", e.OccurredOn, ct);
        logger.LogInformation("Projected TransactionCreditCompletedEvent for {InternalRef}", e.InternalRef);
    }

    private async Task ProjectCreditFailedAsync(TransactionCreditFailedEvent e, CancellationToken ct)
    {
        var tx = await GetOrCreateTransaction(e.InternalRef, ct);

        if (e.PartnerRef is not null)
            tx.PartnerRef = e.PartnerRef;

        var eventId = $"{e.InternalRef}-ca-{e.TotalAttempts}";
        if (await txRepo.CreditAttemptExistsAsync(eventId, ct))
            return;

        txRepo.AddCreditAttempt(new CreditAttempt
        {
            TransactionId = tx.Id,
            AttemptNumber = e.TotalAttempts,
            Gateway = CreditGateway.Humo,
            Status = OperationResult.Failed,
            FailureCode = e.FailureReason,
            AttemptedAt = new DateTimeOffset(e.OccurredOn, TimeSpan.Zero),
            EventId = eventId,
        });

        await AppendStatusHistory(tx, TransactionStatus.CreditFailed, TransactionStatus.CreditFailed,
            $"Credit failed: {e.FailureReason}", $"credit-failed-{e.TotalAttempts}", e.OccurredOn, ct);
        logger.LogInformation("Projected TransactionCreditFailedEvent for {InternalRef}", e.InternalRef);
    }

    private async Task ProjectCreditFailedRetryAsync(TransactionCreditFailedRetryEvent e, CancellationToken ct)
    {
        var tx = await GetOrCreateTransaction(e.InternalRef, ct);

        txRepo.AddCreditAttempt(new CreditAttempt
        {
            TransactionId = tx.Id,
            AttemptNumber = e.Attempt,
            Gateway = CreditGateway.Humo,
            Status = OperationResult.Failed,
            FailureCode = e.FailureReason,
            AttemptedAt = new DateTimeOffset(e.OccurredOn, TimeSpan.Zero),
            EventId = $"{e.InternalRef}-ca-{e.Attempt}",
        });

        await AppendStatusHistory(tx, TransactionStatus.CreditFailedRetry, TransactionStatus.CreditFailedRetry,
            $"Credit failed, will retry: {e.FailureReason}", $"credit-failed-retry-{e.Attempt}", e.OccurredOn, ct);
        logger.LogInformation("Projected TransactionCreditFailedRetryEvent for {InternalRef}", e.InternalRef);
    }

    private async Task ProjectCreditRetryRequestedAsync(TransactionCreditRetryRequestedEvent e, CancellationToken ct)
    {
        var tx = await GetOrCreateTransaction(e.InternalRef, ct);
        await AppendStatusHistory(tx, TransactionStatus.CreditFailedRetry, TransactionStatus.CreditFailedRetry,
            "Credit retry requested after unpause", "credit-retry-requested", e.OccurredOn, ct);
        logger.LogInformation("Projected TransactionCreditRetryRequestedEvent for {InternalRef}", e.InternalRef);
    }

    private async Task ProjectRegistrationCompletedAsync(TransactionRegistrationCompletedEvent e, CancellationToken ct)
    {
        var tx = await GetOrCreateTransaction(e.InternalRef, ct);

        var eventId = $"{e.InternalRef}-pr-{e.RemitterPartnerCode}-{e.Attempt}";
        if (await txRepo.PartnerRegistrationExistsAsync(eventId, ct))
            return;

        txRepo.AddPartnerRegistration(new PartnerRegistration
        {
            TransactionId = tx.Id,
            PartnerName = e.RemitterPartnerCode,
            Status = OperationResult.Succeeded,
            RegisteredAt = new DateTimeOffset(e.OccurredOn, TimeSpan.Zero),
            EventId = eventId,
        });

        await AppendStatusHistory(tx, TransactionStatus.RegistrationSucceeded, TransactionStatus.RegistrationSucceeded,
            $"Registration succeeded with {e.RemitterPartnerCode} (attempt {e.Attempt})",
            $"registration-completed-{e.RemitterPartnerCode}-{e.Attempt}", e.OccurredOn, ct);
        logger.LogInformation("Projected TransactionRegistrationCompletedEvent for {InternalRef}", e.InternalRef);
    }

    private async Task ProjectRegistrationFailedRetryAsync(TransactionRegistrationFailedRetryEvent e, CancellationToken ct)
    {
        var tx = await GetOrCreateTransaction(e.InternalRef, ct);

        var eventId = $"{e.InternalRef}-pr-{e.RemitterPartnerCode}-{e.Attempt}";
        if (await txRepo.PartnerRegistrationExistsAsync(eventId, ct))
            return;

        txRepo.AddPartnerRegistration(new PartnerRegistration
        {
            TransactionId = tx.Id,
            PartnerName = e.RemitterPartnerCode,
            Status = OperationResult.Failed,
            FailureReason = e.FailureReason,
            RegisteredAt = new DateTimeOffset(e.OccurredOn, TimeSpan.Zero),
            EventId = eventId,
        });

        await AppendStatusHistory(tx, TransactionStatus.RegistrationFailedRetry, TransactionStatus.RegistrationFailedRetry,
            $"Registration failed with {e.RemitterPartnerCode}: {e.FailureReason}",
            $"registration-failed-retry-{e.RemitterPartnerCode}-{e.Attempt}", e.OccurredOn, ct);
        logger.LogInformation("Projected TransactionRegistrationFailedRetryEvent for {InternalRef}", e.InternalRef);
    }

    private async Task ProjectRegistrationRetryRequestedAsync(TransactionRegistrationRetryRequestedEvent e, CancellationToken ct)
    {
        var tx = await GetOrCreateTransaction(e.InternalRef, ct);
        await AppendStatusHistory(tx, TransactionStatus.RegistrationFailedRetry, TransactionStatus.RegistrationFailedRetry,
            "Registration retry requested after unpause", "registration-retry-requested", e.OccurredOn, ct);
        logger.LogInformation("Projected TransactionRegistrationRetryRequestedEvent for {InternalRef}", e.InternalRef);
    }

    private async Task ProjectPausedAsync(TransactionPausedEvent e, CancellationToken ct)
    {
        var tx = await GetOrCreateTransaction(e.InternalRef, ct);
        tx.IsPaused = true;

        await AppendStatusHistory(tx, e.StatusBeforePause, TransactionStatus.Paused,
            $"Paused: {e.Reason}{(e.Details is not null ? $" — {e.Details}" : "")}",
            "paused", e.OccurredOn, ct);
        logger.LogInformation("Projected TransactionPausedEvent for {InternalRef}", e.InternalRef);
    }

    private async Task ProjectUnpausedAsync(TransactionUnpausedEvent e, CancellationToken ct)
    {
        var tx = await GetOrCreateTransaction(e.InternalRef, ct);
        tx.IsPaused = false;

        await AppendStatusHistory(tx, TransactionStatus.Paused, e.ResumedToStatus,
            $"Unpaused, resuming to {e.ResumedToStatus}",
            "unpaused", e.OccurredOn, ct);
        logger.LogInformation("Projected TransactionUnpausedEvent for {InternalRef}", e.InternalRef);
    }
}
