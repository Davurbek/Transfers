using Transfers.Dashboard.Domain.Entities.Auth;

namespace Transfers.Dashboard.DataAccess.Repositories;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    /// <summary>Looks up a refresh token by its hash, including the owning user.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
}
