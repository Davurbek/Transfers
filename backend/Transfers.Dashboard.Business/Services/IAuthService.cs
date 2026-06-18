using Transfers.Dashboard.Business.Dtos;

namespace Transfers.Dashboard.Business.Services;

public interface IAuthService
{
    /// <summary>Returns auth tokens + user info, or null on invalid credentials.</summary>
    Task<AuthResult?> LoginAsync(string username, string password, string? ip, CancellationToken ct = default);

    /// <summary>Rotates the refresh token and issues a new access token, or null if invalid.</summary>
    Task<AuthResult?> RefreshAsync(string rawRefreshToken, string? ip, CancellationToken ct = default);

    Task LogoutAsync(string rawRefreshToken, CancellationToken ct = default);

    Task<UserInfo?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
}
