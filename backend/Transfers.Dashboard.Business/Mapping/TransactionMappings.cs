using Transfers.Dashboard.Business.Dtos;
using Transfers.Dashboard.Domain.Entities.Audit;
using Transfers.Dashboard.Domain.Entities.Transactions;

namespace Transfers.Dashboard.Business.Mapping;

/// <summary>Entity → DTO mapping (kept explicit and centralised).</summary>
public static class TransactionMappings
{
    public static TransactionSummaryDto ToSummary(this Transaction t) => new(
        t.TransactionId, t.UserId, t.RecipientName, t.Amount, t.Currency, t.Corridor,
        t.CurrentStatus.ToString(), t.IsPaused, t.CreatedAt, t.UpdatedAt);

    public static TransactionDetailDto ToDetail(this Transaction t) => new(
        t.ToSummary(),
        t.StatusHistory.OrderBy(h => h.OccurredAt)
            .Select(h => new StatusHistoryDto(
                h.FromStatus?.ToString(), h.ToStatus.ToString(), h.Reason, h.OccurredAt))
            .ToList(),
        t.CreditAttempts.OrderBy(c => c.AttemptNumber)
            .Select(c => new CreditAttemptDto(
                c.AttemptNumber, c.Gateway.ToString(), c.Status.ToString(),
                c.FailureCode, c.GatewayResponse, c.AttemptedAt))
            .ToList(),
        t.PartnerRegistrations.OrderBy(p => p.RegisteredAt)
            .Select(p => new PartnerRegistrationDto(
                p.PartnerName, p.Status.ToString(), p.FailureReason, p.ReferenceId, p.RegisteredAt))
            .ToList());

    public static AuditLogDto ToDto(this AuditLog a) => new(
        a.Id, a.Username, a.ActionType, a.TargetTransactionId, a.Timestamp, a.IpAddress, a.Metadata);
}
