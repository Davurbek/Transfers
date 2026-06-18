using Transfers.Dashboard.Business.Dtos;
using Transfers.Dashboard.Domain.Common;

namespace Transfers.Dashboard.Business.Services;

public interface IAuditService
{
    /// <summary>Filtered + paginated audit-log query.</summary>
    Task<PagedResult<AuditLogDto>> SearchAsync(AuditFilter filter, CancellationToken ct = default);

    /// <summary>Writes an immutable audit entry for a privileged write action.</summary>
    Task RecordAsync(AuditEntry entry, CancellationToken ct = default);
}
