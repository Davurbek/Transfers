namespace Universal.Transfers.Application.Audit.DTOs;

public record AuditEntryDto(
    Guid Id,
    string Username,
    string ActionType,
    string? TargetTransactionId,
    DateTimeOffset Timestamp,
    string IpAddress,
    string? Metadata);
