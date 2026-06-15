using Transfers.Dashboard.Api.Domain.Transactions;

namespace Transfers.Dashboard.Api.Messaging;

// ---------------- Events (Main App -> Dashboard) ----------------

/// <summary>Base marker for lifecycle events consumed from the broker.</summary>
public abstract record TransferEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Full snapshot upsert of a transaction.</summary>
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

/// <summary>A single state transition.</summary>
public record TransactionStatusChanged : TransferEvent
{
    public required string TransactionId { get; init; }
    public TransactionStatus? FromStatus { get; init; }
    public required TransactionStatus ToStatus { get; init; }
    public string? Reason { get; init; }
    public bool IsPaused { get; init; }
}

// ---------------- Commands (Dashboard -> Main App) ----------------

/// <summary>Base marker for commands published to the protected queue.</summary>
public abstract record TransferCommand
{
    public string CommandId { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset IssuedAt { get; init; } = DateTimeOffset.UtcNow;
    public required string IssuedByUser { get; init; }
}

/// <summary>Request the Main App to release a paused transaction.</summary>
public record UnpauseTransactionCommand : TransferCommand
{
    public required string TransactionId { get; init; }
}
