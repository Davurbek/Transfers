using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.Transactions.Enums;
using static Universal.Transfers.Domain.Transactions.Enums.TransactionStatus;

namespace Universal.Transfers.Infrastructure.Messaging.MassTransit;

public static class EventRouter
{
    public static TransferEvent? Route(object @event) => @event switch
    {
        // ── TransactionInitiatedEvent → TransactionUpserted ──────────────────
        ApiV2.TransactionInitiatedEvent e => new TransactionUpserted
        {
            InternalRef = e.InternalRef,
            TransactionId = e.InternalRef,
            RecipientName = e.ReceiverCardLast4,
            Amount = e.CreditAmount,
            Currency = e.CreditAmountCurrency,
            Corridor = e.RemitterPartner.ToString(),
            CurrentStatus = ConfirmSucceeded,
            IsPaused = false,
            CreatedAt = e.OccurredOn,
            UpdatedAt = e.OccurredOn,
            CreditGateway = e.PaymentPartner switch
            {
                1 => CreditGateway.Uzcard,
                2 => CreditGateway.Humo,
                _ => CreditGateway.Humo,
            },
            RemitterPartner = ((ApiV2.RemitterPartner)e.RemitterPartner).ToString(),
        },

        // ── TransactionCreditCompletedEvent → status: CreditSucceeded ────────
        ApiV2.TransactionCreditCompletedEvent e => NewStatus(e.InternalRef, e.OccurredOn, CreditSucceeded,
            $"Credit completed (attempt {e.Attempt})", attemptNumber: e.Attempt),

        // ── TransactionCreditFailedEvent → status: CreditFailed ──────────────
        ApiV2.TransactionCreditFailedEvent e => NewStatus(e.InternalRef, e.OccurredOn, CreditFailed,
            $"Credit terminally failed: {e.FailureReason}", attemptNumber: e.TotalAttempts, failureReason: e.FailureReason),

        // ── TransactionCreditFailedRetryEvent → status: CreditFailedRetry ────
        ApiV2.TransactionCreditFailedRetryEvent e => NewStatus(e.InternalRef, e.OccurredOn, CreditFailedRetry,
            $"Credit failed (attempt {e.Attempt}): {e.FailureReason}", attemptNumber: e.Attempt, failureReason: e.FailureReason),

        // ── TransactionCreditRetryRequestedEvent → status: CreditFailedRetry ─
        ApiV2.TransactionCreditRetryRequestedEvent e => NewStatus(e.InternalRef, e.OccurredOn, CreditFailedRetry,
            "Credit retry requested after unpause"),

        // ── TransactionRegistrationCompletedEvent → status: RegistrationSucceeded ─
        ApiV2.TransactionRegistrationCompletedEvent e => NewStatus(e.InternalRef, e.OccurredOn, RegistrationSucceeded,
            $"Registration completed (attempt {e.Attempt})", attemptNumber: e.Attempt,
            partnerName: ((ApiV2.RemitterPartner)e.RemitterPartner).ToString()),

        // ── TransactionRegistrationFailedRetryEvent → status: RegistrationFailedRetry ─
        ApiV2.TransactionRegistrationFailedRetryEvent e => NewStatus(e.InternalRef, e.OccurredOn, RegistrationFailedRetry,
            $"Registration failed (attempt {e.Attempt}): {e.FailureReason}", attemptNumber: e.Attempt,
            failureReason: e.FailureReason, partnerName: ((ApiV2.RemitterPartner)e.RemitterPartner).ToString()),

        // ── TransactionRegistrationRetryRequestedEvent → status: RegistrationFailedRetry ─
        ApiV2.TransactionRegistrationRetryRequestedEvent e => NewStatus(e.InternalRef, e.OccurredOn, RegistrationFailedRetry,
            "Registration retry requested after unpause"),

        // ── TransactionPausedEvent → status: Paused ─────────────────────────
        ApiV2.TransactionPausedEvent e => new TransactionStatusChanged
        {
            InternalRef = e.InternalRef,
            TransactionId = e.InternalRef,
            FromStatus = ToDashboard(e.StatusBeforePause),
            ToStatus = Paused,
            Reason = $"Transaction paused: {e.Details ?? e.Reason.ToString()}",
            IsPaused = true,
            OccurredAt = e.OccurredOn,
        },

        // ── TransactionUnpausedEvent → status: ResumedToStatus ──────────────
        ApiV2.TransactionUnpausedEvent e => NewStatus(e.InternalRef, e.OccurredOn, ToDashboard(e.ResumedToStatus),
            $"Transaction unpaused, resumed to {e.ResumedToStatus}", isPaused: false),

        _ => null,
    };

    private static TransactionStatusChanged NewStatus(string internalRef, DateTime occurredOn, TransactionStatus toStatus,
        string reason, bool isPaused = false, int? attemptNumber = null, string? failureReason = null,
        string? partnerName = null) => new()
    {
        InternalRef = internalRef,
        TransactionId = internalRef,
        FromStatus = null,
        ToStatus = toStatus,
        Reason = reason,
        IsPaused = isPaused,
        OccurredAt = occurredOn,
        AttemptNumber = attemptNumber,
        FailureReason = failureReason,
        PartnerName = partnerName,
    };

    private static TransactionStatus ToDashboard(ApiV2.TransactionStatus s) => s switch
    {
        ApiV2.TransactionStatus.ConfirmPending => ConfirmPending,
        ApiV2.TransactionStatus.ConfirmExpired => ConfirmExpired,
        ApiV2.TransactionStatus.ConfirmFailed => ConfirmFailed,
        ApiV2.TransactionStatus.ConfirmSucceeded => ConfirmSucceeded,
        ApiV2.TransactionStatus.CreditSucceeded => CreditSucceeded,
        ApiV2.TransactionStatus.CreditFailedRetry => CreditFailedRetry,
        ApiV2.TransactionStatus.CreditFailed => CreditFailed,
        ApiV2.TransactionStatus.RegistrationFailedRetry => RegistrationFailedRetry,
        ApiV2.TransactionStatus.RegistrationSucceeded => RegistrationSucceeded,
        ApiV2.TransactionStatus.Paused => Paused,
        _ => Paused,
    };

    // ── ApiV2 event types consumed from Kafka ──────────────────────────────
    public static class ApiV2
    {
        public enum TransactionStatus
        {
            ConfirmPending = 1,
            ConfirmExpired = 2,
            ConfirmFailed = 3,
            ConfirmSucceeded = 4,
            CreditSucceeded = 5,
            CreditFailedRetry = 6,
            CreditFailed = 7,
            RegistrationFailedRetry = 8,
            RegistrationSucceeded = 10,
            Paused = 11,
        }

        public enum TransactionPauseReason
        {
            CreditFailure = 1,
            RegistrationFailure = 2,
            Other = 3,
        }

        public enum PaymentPartner
        {
            Uzcard = 1,
            Humo = 2,
        }

        public enum RemitterPartner
        {
            Tinkoff = 1,
            Profee = 2,
            Gazprom = 3,
            Unlimited = 4,
            MoneyGram = 5,
        }

        public sealed record TransactionInitiatedEvent(
            string InternalRef,
            string? PartnerRef,
            int TransactionType,
            int RemitterPartner,
            int PaymentPartner,
            decimal CreditAmount,
            string CreditAmountCurrency,
            string ReceiverCardLast4,
            DateTime OccurredOn);

        public sealed record TransactionCreditCompletedEvent(
            string InternalRef,
            int Attempt,
            DateTime OccurredOn);

        public sealed record TransactionCreditFailedEvent(
            string InternalRef,
            string? PartnerRef,
            int TotalAttempts,
            string FailureReason,
            DateTime OccurredOn);

        public sealed record TransactionCreditFailedRetryEvent(
            string InternalRef,
            int Attempt,
            string FailureReason,
            DateTime OccurredOn);

        public sealed record TransactionCreditRetryRequestedEvent(
            string InternalRef,
            DateTime OccurredOn);

        public sealed record TransactionRegistrationCompletedEvent(
            string InternalRef,
            int RemitterPartner,
            int Attempt,
            DateTime OccurredOn);

        public sealed record TransactionRegistrationFailedRetryEvent(
            string InternalRef,
            int RemitterPartner,
            int Attempt,
            DateTime NextAttemptAt,
            string FailureReason,
            DateTime OccurredOn);

        public sealed record TransactionRegistrationRetryRequestedEvent(
            string InternalRef,
            DateTime OccurredOn);

        public sealed record TransactionPausedEvent(
            string InternalRef,
            TransactionPauseReason Reason,
            string? Details,
            TransactionStatus StatusBeforePause,
            DateTime OccurredOn);

        public sealed record TransactionUnpausedEvent(
            string InternalRef,
            TransactionStatus ResumedToStatus,
            DateTime OccurredOn);
    }
}
