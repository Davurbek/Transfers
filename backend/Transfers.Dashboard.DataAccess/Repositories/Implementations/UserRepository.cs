using Microsoft.EntityFrameworkCore;
using Transfers.Dashboard.DataAccess.Context;
using Transfers.Dashboard.Domain.Entities.Auth;

namespace Transfers.Dashboard.DataAccess.Repositories.Implementations;

public sealed class UserRepository(DashboardDbContext db) : IUserRepository
{
    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
}
