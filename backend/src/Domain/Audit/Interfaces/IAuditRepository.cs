using Universal.Transfers.Domain.Common;
using Universal.Transfers.Domain.Audit.Entities;

namespace Universal.Transfers.Domain.Audit.Interfaces;

public interface IAuditRepository
{
    Task<PagedResult<AuditLog>> SearchAsync(AuditFilter filter, CancellationToken ct = default);
    Task AddAsync(AuditLog log, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
