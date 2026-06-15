using Transfers.Dashboard.Api.Domain.Transactions;

namespace Transfers.Dashboard.Api.Dtos;

// ---------------- Auth ----------------

public record LoginRequest(string Username, string Password);

public record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    UserInfo User);

public record UserInfo(
    Guid Id,
    string Username,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

// ---------------- Transactions ----------------

public record TransactionSummaryDto(
    string TransactionId,
    string UserId,
    string RecipientName,
    decimal Amount,
    string Currency,
    string Corridor,
    string CurrentStatus,
    bool IsPaused,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record StatusHistoryDto(
    string? FromStatus,
    string ToStatus,
    string? Reason,
    DateTimeOffset OccurredAt);

public record CreditAttemptDto(
    int AttemptNumber,
    string Gateway,
    string Status,
    string? FailureCode,
    string? GatewayResponse,
    DateTimeOffset AttemptedAt);

public record PartnerRegistrationDto(
    string PartnerName,
    string Status,
    string? FailureReason,
    string? ReferenceId,
    DateTimeOffset RegisteredAt);

public record TransactionDetailDto(
    TransactionSummaryDto Summary,
    IReadOnlyList<StatusHistoryDto> StatusHistory,
    IReadOnlyList<CreditAttemptDto> CreditAttempts,
    IReadOnlyList<PartnerRegistrationDto> PartnerRegistrations);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record ActionAcceptedResponse(string Message, string TransactionId, string CommandId);

// ---------------- Audit ----------------

public record AuditLogDto(
    Guid Id,
    string Username,
    string ActionType,
    string? TargetTransactionId,
    DateTimeOffset Timestamp,
    string IpAddress,
    string? Metadata);
