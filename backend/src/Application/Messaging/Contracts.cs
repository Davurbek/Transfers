using Universal.Transfers.Domain.Transactions.Enums;

namespace Universal.Transfers.Application.Messaging;

public abstract record TransferCommand(string CommandId, string IssuedByUser);
public record UnpauseTransactionCommand(string TransactionId, string IssuedByUser) : TransferCommand(Guid.NewGuid().ToString(), IssuedByUser);

public abstract class TransferEvent;
public class TransactionUpserted : TransferEvent
{
    public string TransactionId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Corridor { get; init; } = string.Empty;
    public TransactionStatus CurrentStatus { get; init; }
    public bool IsPaused { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public class TransactionStatusChanged : TransferEvent
{
    public string TransactionId { get; init; } = string.Empty;
    public TransactionStatus FromStatus { get; init; }
    public TransactionStatus ToStatus { get; init; }
    public string Reason { get; init; } = string.Empty;
    public bool IsPaused { get; init; }
}
