using Universal.Transfers.Domain.Common;
using Universal.Transfers.Application.Audit.DTOs;

namespace Universal.Transfers.Application.Audit;

public interface IAuditService
{
    Task<PagedResult<AuditEntryDto>> SearchAsync(
        string? targetTransactionId, string? actionType, string? username,
        DateTimeOffset? fromDate, DateTimeOffset? toDate,
        int page = 1, int pageSize = 50,
        CancellationToken ct = default);
}
