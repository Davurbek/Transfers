using System.Text.Json;
using System.Text.Json.Serialization;
using Universal.Transfers.Domain.PaymentPartners.Enums;
using Universal.Transfers.Domain.Transactions.Enums;

namespace Universal.Transfers.Application.Messaging;

[JsonDerivedType(typeof(TransactionUnpauseRequestedEvent), typeDiscriminator: "TransactionUnpauseRequestedEvent")]
public abstract record TransferCommand(string CommandId, string IssuedByUser);
public record TransactionUnpauseRequestedEvent(string InternalRef, string IssuedByUser) : TransferCommand(Guid.NewGuid().ToString(), IssuedByUser);

[JsonDerivedType(typeof(TransactionInitiatedEvent), typeDiscriminator: "TransactionInitiatedEvent")]
[JsonDerivedType(typeof(TransactionCreditCompletedEvent), typeDiscriminator: "TransactionCreditCompletedEvent")]
[JsonDerivedType(typeof(TransactionCreditFailedEvent), typeDiscriminator: "TransactionCreditFailedEvent")]
[JsonDerivedType(typeof(TransactionCreditFailedRetryEvent), typeDiscriminator: "TransactionCreditFailedRetryEvent")]
[JsonDerivedType(typeof(TransactionRegistrationCompletedEvent), typeDiscriminator: "TransactionRegistrationCompletedEvent")]
[JsonDerivedType(typeof(TransactionRegistrationFailedRetryEvent), typeDiscriminator: "TransactionRegistrationFailedRetryEvent")]
[JsonDerivedType(typeof(TransactionPausedEvent), typeDiscriminator: "TransactionPausedEvent")]
[JsonDerivedType(typeof(TransactionUnpausedEvent), typeDiscriminator: "TransactionUnpausedEvent")]
public abstract record TransferEvent;

public sealed record TransactionInitiatedEvent(
    string InternalRef,
    string? PartnerRef,
    TransactionType TransactionType,
    string RemitterPartnerCode,
    PaymentPartner PaymentPartner,
    decimal CreditAmount,
    string CreditAmountCurrency,
    string ReceiverCardLast4,
    DateTime OccurredOn) : TransferEvent
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record TransactionCreditCompletedEvent(
    string InternalRef,
    int Attempt,
    DateTime OccurredOn) : TransferEvent;

public sealed record TransactionCreditFailedEvent(
    string InternalRef,
    string? PartnerRef,
    int TotalAttempts,
    string FailureReason,
    DateTime OccurredOn) : TransferEvent;

public sealed record TransactionCreditFailedRetryEvent(
    string InternalRef,
    int Attempt,
    string FailureReason,
    DateTime OccurredOn) : TransferEvent;

public sealed record TransactionRegistrationCompletedEvent(
    string InternalRef,
    string RemitterPartnerCode,
    int Attempt,
    DateTime OccurredOn) : TransferEvent;

public sealed record TransactionRegistrationFailedRetryEvent(
    string InternalRef,
    string RemitterPartnerCode,
    int Attempt,
    DateTime NextAttemptAt,
    string FailureReason,
    DateTime OccurredOn) : TransferEvent;

public sealed record TransactionPausedEvent(
    string InternalRef,
    TransactionPauseReason Reason,
    string? Details,
    TransactionStatus StatusBeforePause,
    DateTime OccurredOn) : TransferEvent;

public sealed record TransactionUnpausedEvent(
    string InternalRef,
    TransactionStatus ResumedToStatus,
    DateTime OccurredOn,
    string? PausedTelegramMessageId = null) : TransferEvent;

