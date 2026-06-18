using Microsoft.EntityFrameworkCore;
using Transfers.Dashboard.DataAccess.Context;
using Transfers.Dashboard.Domain.Entities.Auth;

namespace Transfers.Dashboard.DataAccess.Repositories.Implementations;

public sealed class RefreshTokenRepository(DashboardDbContext db) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken ct = default) =>
        await db.RefreshTokens.AddAsync(token, ct);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);
}
