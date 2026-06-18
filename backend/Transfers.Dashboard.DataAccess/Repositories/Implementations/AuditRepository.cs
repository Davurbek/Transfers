using Microsoft.EntityFrameworkCore;
using Transfers.Dashboard.DataAccess.Context;
using Transfers.Dashboard.Domain.Common;
using Transfers.Dashboard.Domain.Entities.Audit;

namespace Transfers.Dashboard.DataAccess.Repositories.Implementations;

public sealed class AuditRepository(DashboardDbContext db) : IAuditRepository
{
    public async Task<PagedResult<AuditLog>> SearchAsync(AuditFilter filter, CancellationToken ct = default)
    {
        var query = db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.TargetTransactionId))
            query = query.Where(a => a.TargetTransactionId == filter.TargetTransactionId);

        if (!string.IsNullOrWhiteSpace(filter.ActionType))
            query = query.Where(a => a.ActionType == filter.ActionType);

        if (!string.IsNullOrWhiteSpace(filter.Username))
            query = query.Where(a => a.Username == filter.Username);

        if (filter.FromDate is { } from)
            query = query.Where(a => a.Timestamp >= from);

        if (filter.ToDate is { } to)
            query = query.Where(a => a.Timestamp <= to);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return new PagedResult<AuditLog>(items, filter.Page, filter.PageSize, total);
    }

    public async Task AddAsync(AuditLog log, CancellationToken ct = default) =>
        await db.AuditLogs.AddAsync(log, ct);
}
