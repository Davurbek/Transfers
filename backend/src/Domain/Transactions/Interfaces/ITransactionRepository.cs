using Universal.Transfers.Domain.Common;
using Universal.Transfers.Domain.Transactions.Entities;
using Universal.Transfers.Domain.Transactions.Enums;

namespace Universal.Transfers.Domain.Transactions.Interfaces;

public interface ITransactionRepository
{
    Task<PagedResult<Transaction>> SearchAsync(TransactionFilter filter, CancellationToken ct = default);
    Task<Transaction?> GetByTransactionIdAsync(string transactionId, CancellationToken ct = default);
    Task<Transaction?> GetByInternalRefAsync(string internalRef, CancellationToken ct = default);
    Task<Transaction?> GetDetailAsync(string transactionId, CancellationToken ct = default);
    Task AddAsync(Transaction transaction, CancellationToken ct = default);
    void AddStatusHistory(TransactionStatusHistory history);
    void AddCreditAttempt(CreditAttempt attempt);
    void AddPartnerRegistration(PartnerRegistration registration);
    Task<bool> StatusEventExistsAsync(string eventId, CancellationToken ct = default);
    Task<bool> CreditAttemptExistsAsync(string eventId, CancellationToken ct = default);
    Task<bool> PartnerRegistrationExistsAsync(string eventId, CancellationToken ct = default);
    Task UpdateCurrentStatusAsync(Guid id, TransactionStatus newStatus, bool? isPaused = null, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    void DetachAddedEntities();
}
