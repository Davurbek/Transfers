using Transfers.Dashboard.Domain.Entities.Transactions;

namespace Transfers.Dashboard.Domain.Common;

/// <summary>Filter + pagination for the transactions list query.</summary>
public sealed class TransactionFilter : PagedQuery
{
    /// <summary>Free-text match against transaction id or recipient name.</summary>
    public string? Search { get; set; }

    public TransactionStatus? Status { get; set; }
    public string? UserId { get; set; }
    public bool? IsPaused { get; set; }
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
}

/// <summary>Filter + pagination for the audit-log query.</summary>
public sealed class AuditFilter : PagedQuery
{
    public string? TargetTransactionId { get; set; }
    public string? ActionType { get; set; }
    public string? Username { get; set; }
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }

    protected override int MaxPageSize => 200;
}
