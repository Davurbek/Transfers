using Transfers.Dashboard.Domain.Entities.Transactions;

namespace Transfers.Dashboard.Business.Messaging;

// ---------------- Events (Main App -> Dashboard) ----------------

public abstract record TransferEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public record TransactionUpserted : TransferEvent
{
    public required string TransactionId { get; init; }
    public required string UserId { get; init; }
    public required string RecipientName { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Corridor { get; init; }
    public required TransactionStatus Status { get; init; }
    public bool IsPaused { get; init; }
}

public record TransactionStatusChanged : TransferEvent
{
    public required string TransactionId { get; init; }
    public TransactionStatus? FromStatus { get; init; }
    public required TransactionStatus ToStatus { get; init; }
    public string? Reason { get; init; }
    public bool IsPaused { get; init; }
}

// ---------------- Commands (Dashboard -> Main App) ----------------

public abstract record TransferCommand
{
    public string CommandId { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset IssuedAt { get; init; } = DateTimeOffset.UtcNow;
    public required string IssuedByUser { get; init; }
}

public record UnpauseTransactionCommand : TransferCommand
{
    public required string TransactionId { get; init; }
}
