namespace Transfers.Dashboard.Business.Dtos;

public record AuditLogDto(
    Guid Id,
    string Username,
    string ActionType,
    string? TargetTransactionId,
    DateTimeOffset Timestamp,
    string IpAddress,
    string? Metadata);

/// <summary>Context the API passes to the service to record an audit entry.</summary>
public record AuditEntry(
    Guid UserId,
    string Username,
    string ActionType,
    string? TargetTransactionId,
    string IpAddress,
    string? Metadata = null);
