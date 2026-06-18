using Transfers.Dashboard.Business.Dtos;
using Transfers.Dashboard.Domain.Common;

namespace Transfers.Dashboard.Business.Services;

public interface ITransactionService
{
    /// <summary>Filter + pagination read query for the UI.</summary>
    Task<PagedResult<TransactionSummaryDto>> SearchAsync(TransactionFilter filter, CancellationToken ct = default);

    /// <summary>Full lifecycle/credit/partner detail for one transaction.</summary>
    Task<TransactionDetailDto?> GetDetailAsync(string transactionId, CancellationToken ct = default);

    /// <summary>
    /// Requests an unpause: writes an immutable audit entry and publishes a command
    /// to the Main App. Never mutates the transaction directly.
    /// </summary>
    Task<UnpauseResult> RequestUnpauseAsync(UnpauseRequest request, CancellationToken ct = default);
}
