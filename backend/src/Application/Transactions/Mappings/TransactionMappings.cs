using Universal.Transfers.Domain.Transactions.Entities;
using Universal.Transfers.Application.Transactions.DTOs;

namespace Universal.Transfers.Application.Transactions.Mappings;

public static class TransactionMappings
{
    public static TransactionListItemDto ToListItemDto(Transaction tx) => new(
        tx.TransactionId, tx.UserId, tx.RecipientName, tx.Amount, tx.Currency,
        tx.Corridor, tx.CurrentStatus, tx.IsPaused, tx.CreatedAt, tx.UpdatedAt);

    public static TransactionDetailDto ToDetailDto(Transaction tx) => new(
        tx.TransactionId, tx.UserId, tx.RecipientName, tx.Amount, tx.Currency,
        tx.Corridor, tx.CurrentStatus, tx.IsPaused, tx.CreatedAt, tx.UpdatedAt,
        tx.StatusHistory.Select(h => new StatusHistoryDto(h.FromStatus, h.ToStatus, h.Reason, h.OccurredAt)).ToList(),
        tx.CreditAttempts.Select(c => new CreditAttemptDto(c.AttemptNumber, c.Gateway, c.Status, c.FailureCode, c.GatewayResponse, c.AttemptedAt)).ToList(),
        tx.PartnerRegistrations.Select(p => new PartnerRegistrationDto(p.PartnerName, p.Status, p.FailureReason, p.ReferenceId, p.RegisteredAt)).ToList());
}
