using Universal.Transfers.Domain.Common;
using Universal.Transfers.Domain.Audit.Interfaces;
using Universal.Transfers.Application.Audit.DTOs;

namespace Universal.Transfers.Application.Audit;

public class AuditService(IAuditRepository auditRepo) : IAuditService
{
    public async Task<PagedResult<AuditEntryDto>> SearchAsync(
        string? targetTransactionId, string? actionType, string? username,
        DateTimeOffset? fromDate, DateTimeOffset? toDate,
        int page = 1, int pageSize = 50,
        CancellationToken ct = default)
    {
        var filter = new AuditFilter
        {
            TargetTransactionId = targetTransactionId,
            ActionType = actionType,
            Username = username,
            FromDate = fromDate,
            ToDate = toDate,
            Page = page,
            PageSize = pageSize,
        };

        var paged = await auditRepo.SearchAsync(filter, ct);
        return paged.Map(e => new AuditEntryDto(e.Id, e.Username, e.ActionType, e.TargetTransactionId, e.Timestamp, e.IpAddress, e.Metadata));
    }
}
