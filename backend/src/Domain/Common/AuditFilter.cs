namespace Universal.Transfers.Domain.Common;

public sealed class AuditFilter : PagedQuery
{
    public string? TargetTransactionId { get; set; }
    public string? ActionType { get; set; }
    public string? Username { get; set; }
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }

    protected override int MaxPageSize => 200;
}
