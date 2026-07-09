using Universal.Transfers.Application.Messaging;
using Universal.Transfers.Domain.Transactions.Enums;

namespace Universal.Transfers.Infrastructure.Messaging.MassTransit;

public static class EventRouter
{
    public static TransferEvent? Route(object @event) => @event switch
    {
        // ── Direct mapping to our new dashboard event types ──────────────────
        TransactionInitiatedEvent e => e,
        TransactionCreditCompletedEvent e => e,
        TransactionCreditFailedEvent e => e,
        TransactionCreditFailedRetryEvent e => e,
        TransactionCreditRetryRequestedEvent e => e,
        TransactionRegistrationCompletedEvent e => e,
        TransactionRegistrationFailedRetryEvent e => e,
        TransactionRegistrationRetryRequestedEvent e => e,
        TransactionPausedEvent e => e,
        TransactionUnpausedEvent e => e,

        // ── ApiV2 events (from MassTransit Kafka rider) ─────────────────────
        ApiV2.TransactionInitiatedEvent e => new TransactionInitiatedEvent(
            e.InternalRef, e.PartnerRef, "standard", e.RemitterPartner.ToString(),
            e.PaymentPartner.ToString(), e.CreditAmount, e.CreditAmountCurrency,
            e.ReceiverCardLast4, e.OccurredOn),

        ApiV2.TransactionCreditCompletedEvent e =>
            new TransactionCreditCompletedEvent(e.InternalRef, e.Attempt, e.OccurredOn),

        ApiV2.TransactionCreditFailedEvent e =>
            new TransactionCreditFailedEvent(e.InternalRef, e.PartnerRef,
                e.TotalAttempts, e.FailureReason, e.OccurredOn),

        ApiV2.TransactionCreditFailedRetryEvent e =>
            new TransactionCreditFailedRetryEvent(e.InternalRef, e.Attempt,
                e.FailureReason, e.OccurredOn),

        ApiV2.TransactionCreditRetryRequestedEvent e =>
            new TransactionCreditRetryRequestedEvent(e.InternalRef, e.OccurredOn),

        ApiV2.TransactionRegistrationCompletedEvent e =>
            new TransactionRegistrationCompletedEvent(e.InternalRef,
                e.RemitterPartner.ToString(), e.Attempt, e.OccurredOn),

        ApiV2.TransactionRegistrationFailedRetryEvent e =>
            new TransactionRegistrationFailedRetryEvent(e.InternalRef,
                e.RemitterPartner.ToString(), e.Attempt, e.NextAttemptAt,
                e.FailureReason, e.OccurredOn),

        ApiV2.TransactionRegistrationRetryRequestedEvent e =>
            new TransactionRegistrationRetryRequestedEvent(e.InternalRef, e.OccurredOn),

        ApiV2.TransactionPausedEvent e =>
            new TransactionPausedEvent(e.InternalRef, e.Reason.ToString(),
                e.Details, ToDashboard(e.StatusBeforePause), e.OccurredOn),

        ApiV2.TransactionUnpausedEvent e =>
            new TransactionUnpausedEvent(e.InternalRef,
                ToDashboard(e.ResumedToStatus), e.OccurredOn),

        _ => null,
    };

    private static TransactionStatus ToDashboard(ApiV2.TransactionStatus s) => s switch
    {
        ApiV2.TransactionStatus.ConfirmPending => TransactionStatus.ConfirmPending,
        ApiV2.TransactionStatus.ConfirmExpired => TransactionStatus.ConfirmExpired,
        ApiV2.TransactionStatus.ConfirmFailed => TransactionStatus.ConfirmFailed,
        ApiV2.TransactionStatus.ConfirmSucceeded => TransactionStatus.ConfirmSucceeded,
        ApiV2.TransactionStatus.CreditSucceeded => TransactionStatus.CreditSucceeded,
        ApiV2.TransactionStatus.CreditFailedRetry => TransactionStatus.CreditFailedRetry,
        ApiV2.TransactionStatus.CreditFailed => TransactionStatus.CreditFailed,
        ApiV2.TransactionStatus.RegistrationFailedRetry => TransactionStatus.RegistrationFailedRetry,
        ApiV2.TransactionStatus.RegistrationSucceeded => TransactionStatus.RegistrationSucceeded,
        ApiV2.TransactionStatus.Paused => TransactionStatus.Paused,
        _ => TransactionStatus.Paused,
    };

    public static class ApiV2
    {
        public enum TransactionStatus
        {
            ConfirmPending = 1, ConfirmExpired = 2, ConfirmFailed = 3,
            ConfirmSucceeded = 4, CreditSucceeded = 5, CreditFailedRetry = 6,
            CreditFailed = 7, RegistrationFailedRetry = 8,
            RegistrationSucceeded = 10, Paused = 11,
        }

        public enum TransactionPauseReason { CreditFailure = 1, RegistrationFailure = 2, Other = 3, }
        public enum PaymentPartner { Uzcard = 1, Humo = 2, }
        public enum RemitterPartner { Tinkoff = 1, Profee = 2, Gazprom = 3, Unlimited = 4, MoneyGram = 5, }

        public sealed record TransactionInitiatedEvent(
            string InternalRef, string? PartnerRef, int TransactionType,
            int RemitterPartner, int PaymentPartner, decimal CreditAmount,
            string CreditAmountCurrency, string ReceiverCardLast4, DateTime OccurredOn);

        public sealed record TransactionCreditCompletedEvent(
            string InternalRef, int Attempt, DateTime OccurredOn);

        public sealed record TransactionCreditFailedEvent(
            string InternalRef, string? PartnerRef, int TotalAttempts,
            string FailureReason, DateTime OccurredOn);

        public sealed record TransactionCreditFailedRetryEvent(
            string InternalRef, int Attempt, string FailureReason, DateTime OccurredOn);

        public sealed record TransactionCreditRetryRequestedEvent(
            string InternalRef, DateTime OccurredOn);

        public sealed record TransactionRegistrationCompletedEvent(
            string InternalRef, int RemitterPartner, int Attempt, DateTime OccurredOn);

        public sealed record TransactionRegistrationFailedRetryEvent(
            string InternalRef, int RemitterPartner, int Attempt,
            DateTime NextAttemptAt, string FailureReason, DateTime OccurredOn);

        public sealed record TransactionRegistrationRetryRequestedEvent(
            string InternalRef, DateTime OccurredOn);

        public sealed record TransactionPausedEvent(
            string InternalRef, TransactionPauseReason Reason, string? Details,
            TransactionStatus StatusBeforePause, DateTime OccurredOn);

        public sealed record TransactionUnpausedEvent(
            string InternalRef, TransactionStatus ResumedToStatus, DateTime OccurredOn);
    }
}
