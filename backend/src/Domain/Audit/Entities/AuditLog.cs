namespace Universal.Transfers.Domain.Audit.Entities;

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
