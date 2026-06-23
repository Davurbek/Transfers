using Microsoft.EntityFrameworkCore;
using Universal.Transfers.Domain.Common;
using Universal.Transfers.Domain.Audit.Entities;
using Universal.Transfers.Domain.Audit.Interfaces;
using Universal.Transfers.Infrastructure.Common.Persistence;

namespace Universal.Transfers.Infrastructure.Audit.Persistence;

public sealed class AuditRepository(AppDbContext db) : IAuditRepository
{
    public async Task<PagedResult<AuditLog>> SearchAsync(AuditFilter filter, CancellationToken ct = default)
    {
        var query = db.AuditLogs.AsNoTracking();

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

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
