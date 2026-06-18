using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Transfers.Dashboard.DataAccess.Repositories;
using Transfers.Dashboard.Domain.Entities.Auth;

namespace Transfers.Dashboard.Business.Auth;

/// <summary>
/// Issues stateless JWT access tokens (with a flattened permission set) and
/// server-side refresh tokens stored as hashes for instant revocability.
/// </summary>
public sealed class TokenService(
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPermissionService permissionService,
    IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _opt = options.Value;

    public async Task<AccessToken> CreateAccessTokenAsync(User user, CancellationToken ct = default)
    {
        var permissions = await permissionService.GetEffectivePermissionsAsync(user.Id, ct);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(permissions.Select(p => new Claim(CustomClaims.Permission, p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTimeOffset.UtcNow.AddMinutes(_opt.AccessTokenMinutes);

        var jwt = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }

    public async Task<IssuedRefreshToken> CreateRefreshTokenAsync(User user, string? ip, CancellationToken ct = default)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(raw),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_opt.RefreshTokenDays),
            CreatedByIp = ip,
        };
        await refreshTokens.AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new IssuedRefreshToken(raw, entity.ExpiresAt);
    }

    public async Task<(User user, IssuedRefreshToken rotated)?> RotateRefreshTokenAsync(
        string rawToken, string? ip, CancellationToken ct = default)
    {
        var existing = await refreshTokens.GetByHashAsync(Hash(rawToken), ct);
        if (existing is null || !existing.IsActive || !existing.User.IsActive)
            return null;

        // Rotate: revoke the used token and issue a fresh one.
        existing.RevokedAt = DateTimeOffset.UtcNow;
        var rotated = await CreateRefreshTokenAsync(existing.User, ip, ct); // saves
        return (existing.User, rotated);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken ct = default)
    {
        var existing = await refreshTokens.GetByHashAsync(Hash(rawToken), ct);
        if (existing is { RevokedAt: null })
        {
            existing.RevokedAt = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
