namespace Universal.Transfers.Domain.Transactions.Enums;

/// <summary>
/// Matches Transfers.Core.Enums.Transaction.TransactionPauseReason from the Kafka project (api-v2).
/// </summary>
public enum TransactionPauseReason
{
    CreditFailure = 1,
    RegistrationFailure = 2,
    Other = 3
}
