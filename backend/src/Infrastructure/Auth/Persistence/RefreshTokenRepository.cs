using Microsoft.EntityFrameworkCore;
using Universal.Transfers.Domain.Auth.Entities;
using Universal.Transfers.Domain.Auth.Interfaces;
using Universal.Transfers.Infrastructure.Common.Persistence;

namespace Universal.Transfers.Infrastructure.Auth.Persistence;

public sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken ct = default) =>
        await db.RefreshTokens.AddAsync(token, ct);

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
