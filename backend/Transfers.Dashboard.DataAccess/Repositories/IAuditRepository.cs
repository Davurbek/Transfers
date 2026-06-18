using Transfers.Dashboard.Domain.Common;
using Transfers.Dashboard.Domain.Entities.Audit;

namespace Transfers.Dashboard.DataAccess.Repositories;

public interface IAuditRepository
{
    /// <summary>Filtered + paginated audit-log query.</summary>
    Task<PagedResult<AuditLog>> SearchAsync(AuditFilter filter, CancellationToken ct = default);

    Task AddAsync(AuditLog log, CancellationToken ct = default);
}
