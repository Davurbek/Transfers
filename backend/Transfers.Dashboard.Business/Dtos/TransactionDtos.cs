namespace Transfers.Dashboard.Business.Dtos;

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

// ---- Unpause write action ----

public enum UnpauseOutcome
{
    Accepted,
    NotFound,
    NotPaused
}

/// <summary>Context the API passes to the service for an unpause request.</summary>
public record UnpauseRequest(string TransactionId, Guid UserId, string Username, string IpAddress);

public record UnpauseResult(UnpauseOutcome Outcome, string? CommandId);
