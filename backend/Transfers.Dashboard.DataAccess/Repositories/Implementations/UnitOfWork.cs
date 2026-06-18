using Transfers.Dashboard.DataAccess.Context;

namespace Transfers.Dashboard.DataAccess.Repositories.Implementations;

public sealed class UnitOfWork(DashboardDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
