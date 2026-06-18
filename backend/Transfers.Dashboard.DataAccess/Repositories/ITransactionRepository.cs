using Transfers.Dashboard.Domain.Common;
using Transfers.Dashboard.Domain.Entities.Transactions;

namespace Transfers.Dashboard.DataAccess.Repositories;

public interface ITransactionRepository
{
    /// <summary>Filtered + paginated transaction search (read-only).</summary>
    Task<PagedResult<Transaction>> SearchAsync(TransactionFilter filter, CancellationToken ct = default);

    /// <summary>Single transaction without related collections.</summary>
    Task<Transaction?> GetByTransactionIdAsync(string transactionId, CancellationToken ct = default);

    /// <summary>Single transaction with status history, credit attempts and partner registrations.</summary>
    Task<Transaction?> GetDetailAsync(string transactionId, CancellationToken ct = default);

    Task AddAsync(Transaction transaction, CancellationToken ct = default);

    void AddStatusHistory(TransactionStatusHistory history);

    /// <summary>Idempotency guard for consumed status-change events.</summary>
    Task<bool> StatusEventExistsAsync(string eventId, CancellationToken ct = default);
}
