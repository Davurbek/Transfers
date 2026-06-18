namespace Transfers.Dashboard.Domain.Entities.Audit;

/// <summary>
/// Immutable record of a privileged write action performed via the dashboard.
/// Append-only: never updated or deleted by the API.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string? TargetTransactionId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string IpAddress { get; set; } = string.Empty;
    public string? Metadata { get; set; }
}
