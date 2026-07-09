using Universal.Transfers.Domain.Transactions.Entities;
using Universal.Transfers.Application.Transactions.DTOs;

namespace Universal.Transfers.Application.Transactions.Mappings;

public static class TransactionMappings
{
    public static TransactionListItemDto ToListItemDto(Transaction tx) => new(
        tx.InternalRef, tx.TransactionId, tx.UserId, tx.RecipientName, tx.Amount, tx.Currency,
        tx.Corridor, tx.CurrentStatus, tx.IsPaused, tx.CreatedAt, tx.UpdatedAt, tx.PaymentPartner);

    public static TransactionDetailDto ToDetailDto(Transaction tx) => new(
        tx.InternalRef, tx.TransactionId, tx.UserId, tx.RecipientName, tx.Amount, tx.Currency,
        tx.Corridor, tx.CurrentStatus, tx.IsPaused, tx.CreatedAt, tx.UpdatedAt, tx.PaymentPartner,
        tx.StatusHistory.OrderBy(h => h.OccurredAt).ThenBy(h => h.Id)
            .Select(h => new StatusHistoryDto(h.FromStatus, h.ToStatus, h.Reason, h.OccurredAt)).ToList(),
        tx.CreditAttempts.OrderBy(c => c.AttemptedAt).ThenBy(c => c.Id)
            .Select(c => new CreditAttemptDto(c.AttemptNumber, c.Gateway, c.Status, c.FailureCode, c.GatewayResponse, c.AttemptedAt)).ToList(),
        tx.PartnerRegistrations.OrderBy(p => p.RegisteredAt).ThenBy(p => p.Id)
            .Select(p => new PartnerRegistrationDto(p.PartnerName, p.Status, p.FailureReason, p.ReferenceId, p.RegisteredAt)).ToList());
}
