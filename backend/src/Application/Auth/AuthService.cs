using Microsoft.Extensions.Options;
using Universal.Transfers.Application.Auth.DTOs;
using Universal.Transfers.Domain.Auth.Interfaces;
using Universal.Transfers.Domain.Common.Security;

namespace Universal.Transfers.Application.Auth;

public class AuthService(
    IUserRepository userRepo,
    IRefreshTokenRepository refreshTokenRepo,
    IPermissionService permissionService,
    ITokenService tokenService,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<LoginServiceResult> LoginAsync(string username, string password, string? ipAddress, CancellationToken ct = default)
    {
        var user = await userRepo.GetByUsernameAsync(username, ct);
        if (user is null || !user.IsActive || !PasswordHasher.Verify(password, user.PasswordHash))
            return new LoginServiceResult(false, null, null, "Invalid credentials");

        var permissions = (await permissionService.GetUserPermissionsAsync(user.Id, ct)).ToList();
        var roles = (await permissionService.GetUserRolesAsync(user.Id, ct)).ToList();

        var accessToken = tokenService.GenerateAccessToken(user, permissions, roles);
        var rawRefresh = tokenService.GenerateRefreshToken();
        var refreshTokenHash = tokenService.HashToken(rawRefresh);

        await refreshTokenRepo.AddAsync(new Domain.Auth.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedByIp = ipAddress,
        }, ct);

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);
        var userInfo = new UserInfoResponse(user.Id, user.Username, user.Email, permissions, roles);
        return new LoginServiceResult(true, new LoginResponse(accessToken, expiresAt, userInfo), rawRefresh, null);
    }

    public async Task<LoginServiceResult> RefreshTokenAsync(string refreshToken, string? ipAddress, CancellationToken ct = default)
    {
        var hash = tokenService.HashToken(refreshToken);
        var stored = await refreshTokenRepo.GetByHashAsync(hash, ct);
        if (stored is null || !stored.IsActive)
            return new LoginServiceResult(false, null, null, "Invalid or expired refresh token");

        stored.RevokedAt = DateTimeOffset.UtcNow;

        var user = stored.User;
        var permissions = (await permissionService.GetUserPermissionsAsync(user.Id, ct)).ToList();
        var roles = (await permissionService.GetUserRolesAsync(user.Id, ct)).ToList();

        var accessToken = tokenService.GenerateAccessToken(user, permissions, roles);
        var rawRefresh = tokenService.GenerateRefreshToken();
        var refreshTokenHash = tokenService.HashToken(rawRefresh);

        await refreshTokenRepo.AddAsync(new Domain.Auth.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedByIp = ipAddress,
        }, ct);

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);
        var userInfo = new UserInfoResponse(user.Id, user.Username, user.Email, permissions, roles);
        return new LoginServiceResult(true, new LoginResponse(accessToken, expiresAt, userInfo), rawRefresh, null);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = tokenService.HashToken(refreshToken);
        var stored = await refreshTokenRepo.GetByHashAsync(hash, ct);
        if (stored is not null)
            stored.RevokedAt = DateTimeOffset.UtcNow;
    }

    public async Task<UserInfoResponse?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepo.GetByIdAsync(userId, ct);
        if (user is null) return null;

        var permissions = await permissionService.GetUserPermissionsAsync(userId, ct);
        var roles = await permissionService.GetUserRolesAsync(userId, ct);
        return new UserInfoResponse(user.Id, user.Username, user.Email, permissions, roles);
    }
}
