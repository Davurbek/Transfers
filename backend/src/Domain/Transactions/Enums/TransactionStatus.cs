namespace Universal.Transfers.Domain.Transactions.Enums;

/// <summary>
/// Matches Transfers.Core.Enums.Transaction.TransactionStatus from the Kafka project (api-v2).
/// </summary>
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
    CreditPending = 12,
    RegistrationPending = 13,
    Cancelled = 14,
}
