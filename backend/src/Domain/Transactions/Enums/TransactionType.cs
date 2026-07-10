namespace Universal.Transfers.Domain.Transactions.Enums;

/// <summary>
/// Matches Transfers.Core.Enums.Transaction.TransactionType from the Kafka project (api-v2).
/// </summary>
public enum TransactionType
{
    Local = 0,
    International = 1
}
