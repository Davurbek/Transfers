namespace Transfers.Dashboard.Domain.Entities.Transactions;

/// <summary>Lifecycle states of a cross-border transfer.</summary>
public enum TransactionStatus
{
    ConfirmPending,
    ConfirmSucceeded,
    ConfirmFailed,
    CreditPending,
    CreditSucceeded,
    CreditFailed,
    RegistrationPending,
    RegistrationFailedRetry,
    RegistrationSucceeded,
    Paused,
    Cancelled
}

public enum CreditGateway
{
    Humo,
    Uzcard
}

public enum OperationResult
{
    Succeeded,
    Failed
}

/// <summary>Read-optimized replica of a transaction. Populated from Main App events.</summary>
public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TransactionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Corridor { get; set; } = string.Empty;
    public TransactionStatus CurrentStatus { get; set; } = TransactionStatus.ConfirmPending;
    public bool IsPaused { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<TransactionStatusHistory> StatusHistory { get; set; } = new List<TransactionStatusHistory>();
    public ICollection<CreditAttempt> CreditAttempts { get; set; } = new List<CreditAttempt>();
    public ICollection<PartnerRegistration> PartnerRegistrations { get; set; } = new List<PartnerRegistration>();
}

/// <summary>One timestamped state transition.</summary>
public class TransactionStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;
    public TransactionStatus? FromStatus { get; set; }
    public TransactionStatus ToStatus { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Idempotency key from the originating broker event.</summary>
    public string? EventId { get; set; }
}

/// <summary>One attempt to credit funds to the local recipient (Humo/Uzcard).</summary>
public class CreditAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;
    public int AttemptNumber { get; set; }
    public CreditGateway Gateway { get; set; }
    public OperationResult Status { get; set; }
    public string? FailureCode { get; set; }
    public string? GatewayResponse { get; set; }
    public DateTimeOffset AttemptedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Result of registering the transfer into a downstream partner network.</summary>
public class PartnerRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;
    public string PartnerName { get; set; } = string.Empty;
    public OperationResult Status { get; set; }
    public string? FailureReason { get; set; }
    public string? ReferenceId { get; set; }
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
}
