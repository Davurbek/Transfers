namespace Transfers.Dashboard.DataAccess.Repositories;

/// <summary>Commits pending changes across repositories that share the DbContext.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
