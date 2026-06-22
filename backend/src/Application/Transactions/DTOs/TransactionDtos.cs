using Universal.Transfers.Domain.Transactions.Enums;

namespace Universal.Transfers.Application.Transactions.DTOs;

public record TransactionListItemDto(
    string InternalRef,
    string TransactionId,
    string UserId,
    string RecipientName,
    decimal Amount,
    string Currency,
    string Corridor,
    TransactionStatus CurrentStatus,
    bool IsPaused,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record TransactionDetailDto(
    string InternalRef,
    string TransactionId,
    string UserId,
    string RecipientName,
    decimal Amount,
    string Currency,
    string Corridor,
    TransactionStatus CurrentStatus,
    bool IsPaused,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<StatusHistoryDto> StatusHistory,
    List<CreditAttemptDto> CreditAttempts,
    List<PartnerRegistrationDto> PartnerRegistrations);

public record StatusHistoryDto(
    TransactionStatus? FromStatus,
    TransactionStatus ToStatus,
    string? Reason,
    DateTimeOffset OccurredAt);

public record CreditAttemptDto(
    int AttemptNumber,
    CreditGateway Gateway,
    OperationResult Status,
    string? FailureCode,
    string? GatewayResponse,
    DateTimeOffset AttemptedAt);

public record PartnerRegistrationDto(
    string PartnerName,
    OperationResult Status,
    string? FailureReason,
    string? ReferenceId,
    DateTimeOffset RegisteredAt);

public enum UnpauseOutcome { Accepted, NotFound, NotPaused }

public record UnpauseResult(UnpauseOutcome Outcome, string? CommandId);
