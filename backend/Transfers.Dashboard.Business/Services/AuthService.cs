using Transfers.Dashboard.Business.Auth;
using Transfers.Dashboard.Business.Dtos;
using Transfers.Dashboard.DataAccess.Repositories;
using Transfers.Dashboard.Domain.Entities.Auth;
using Transfers.Dashboard.Domain.Security;

namespace Transfers.Dashboard.Business.Services;

public sealed class AuthService(
    IUserRepository users,
    ITokenService tokens,
    IPermissionService permissionService) : IAuthService
{
    public async Task<AuthResult?> LoginAsync(string username, string password, string? ip, CancellationToken ct = default)
    {
        var user = await users.GetByUsernameAsync(username, ct);
        if (user is null || !user.IsActive || !PasswordHasher.Verify(password, user.PasswordHash))
            return null;

        return await IssueAsync(user, ip, ct);
    }

    public async Task<AuthResult?> RefreshAsync(string rawRefreshToken, string? ip, CancellationToken ct = default)
    {
        var rotated = await tokens.RotateRefreshTokenAsync(rawRefreshToken, ip, ct);
        if (rotated is null) return null;

        var (user, refresh) = rotated.Value;
        var access = await tokens.CreateAccessTokenAsync(user, ct);
        var info = await BuildUserInfoAsync(user, ct);
        return new AuthResult(access.Value, access.ExpiresAt, refresh.RawValue, refresh.ExpiresAt, info);
    }

    public Task LogoutAsync(string rawRefreshToken, CancellationToken ct = default) =>
        tokens.RevokeAsync(rawRefreshToken, ct);

    public async Task<UserInfo?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct);
        return user is null ? null : await BuildUserInfoAsync(user, ct);
    }

    private async Task<AuthResult> IssueAsync(User user, string? ip, CancellationToken ct)
    {
        var access = await tokens.CreateAccessTokenAsync(user, ct);
        var refresh = await tokens.CreateRefreshTokenAsync(user, ip, ct);
        var info = await BuildUserInfoAsync(user, ct);
        return new AuthResult(access.Value, access.ExpiresAt, refresh.RawValue, refresh.ExpiresAt, info);
    }

    private async Task<UserInfo> BuildUserInfoAsync(User user, CancellationToken ct)
    {
        var roles = await permissionService.GetRoleNamesAsync(user.Id, ct);
        var perms = await permissionService.GetEffectivePermissionsAsync(user.Id, ct);
        return new UserInfo(user.Id, user.Username, user.Email, roles, perms.OrderBy(p => p).ToList());
    }
}
