using Universal.Transfers.Domain.Common;
using Universal.Transfers.Domain.Transactions.Entities;

namespace Universal.Transfers.Domain.Transactions.Interfaces;

public interface ITransactionRepository
{
    Task<PagedResult<Transaction>> SearchAsync(TransactionFilter filter, CancellationToken ct = default);
    Task<Transaction?> GetByTransactionIdAsync(string transactionId, CancellationToken ct = default);
    Task<Transaction?> GetDetailAsync(string transactionId, CancellationToken ct = default);
    Task AddAsync(Transaction transaction, CancellationToken ct = default);
    void AddStatusHistory(TransactionStatusHistory history);
    Task<bool> StatusEventExistsAsync(string eventId, CancellationToken ct = default);
}
