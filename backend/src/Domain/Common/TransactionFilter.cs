using Universal.Transfers.Domain.Transactions.Enums;

namespace Universal.Transfers.Domain.Common;

public sealed class TransactionFilter : PagedQuery
{
    public string? Search { get; set; }
    public TransactionStatus? Status { get; set; }
    public string? UserId { get; set; }
    public bool? IsPaused { get; set; }
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
}
