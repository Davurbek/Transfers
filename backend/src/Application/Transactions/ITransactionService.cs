using Universal.Transfers.Domain.Common;
using Universal.Transfers.Application.Transactions.DTOs;

namespace Universal.Transfers.Application.Transactions;

public interface ITransactionService
{
    Task<PagedResult<TransactionListItemDto>> SearchAsync(
        string? search, string? status, string? userId, bool? isPaused,
        DateTimeOffset? fromDate, DateTimeOffset? toDate,
        int page = 1, int pageSize = 20,
        CancellationToken ct = default);

    Task<TransactionDetailDto?> GetDetailAsync(string transactionId, CancellationToken ct = default);

    Task<UnpauseResult> UnpauseAsync(string transactionId, Guid userId, string username, string ipAddress, CancellationToken ct = default);
}
