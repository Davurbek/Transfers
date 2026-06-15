using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Transfers.Dashboard.Api.Data;
using Transfers.Dashboard.Api.Domain.Auth;

namespace Transfers.Dashboard.Api.Auth;

/// <summary>Custom claim type carrying a single permission string.</summary>
public static class CustomClaims
{
    public const string Permission = "perm";
}

public record AccessToken(string Value, DateTimeOffset ExpiresAt);
public record IssuedRefreshToken(string RawValue, DateTimeOffset ExpiresAt);

/// <summary>
/// Issues stateless JWT access tokens (with a flattened permission set) and
/// server-side refresh tokens stored as hashes for instant revocability.
/// </summary>
public class TokenService(DashboardDbContext db, PermissionResolver resolver, IOptions<JwtOptions> options)
{
    private readonly JwtOptions _opt = options.Value;

    public async Task<AccessToken> CreateAccessTokenAsync(User user, CancellationToken ct = default)
    {
        var permissions = await resolver.GetEffectivePermissionsAsync(user.Id, ct);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        // Flatten the permission set into individual claims.
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

        var value = new JwtSecurityTokenHandler().WriteToken(jwt);
        return new AccessToken(value, expires);
    }

    public async Task<IssuedRefreshToken> CreateRefreshTokenAsync(User user, string? ip, CancellationToken ct = default)
    {
        // Raw token goes to the client cookie; only its hash is persisted.
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(raw),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_opt.RefreshTokenDays),
            CreatedByIp = ip,
        };
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);
        return new IssuedRefreshToken(raw, entity.ExpiresAt);
    }

    /// <summary>Validates a raw refresh token, rotates it, and returns the user.</summary>
    public async Task<(User user, IssuedRefreshToken rotated)?> RotateRefreshTokenAsync(
        string rawToken, string? ip, CancellationToken ct = default)
    {
        var hash = Hash(rawToken);
        var existing = await db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);

        if (existing is null || !existing.IsActive || !existing.User.IsActive)
            return null;

        // Rotate: revoke the used token and issue a fresh one.
        existing.RevokedAt = DateTimeOffset.UtcNow;
        var rotated = await CreateRefreshTokenAsync(existing.User, ip, ct);
        await db.SaveChangesAsync(ct);
        return (existing.User, rotated);
    }

    /// <summary>Revokes a refresh token (logout / session kill). Idempotent.</summary>
    public async Task RevokeAsync(string rawToken, CancellationToken ct = default)
    {
        var hash = Hash(rawToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);
        if (existing is { RevokedAt: null })
        {
            existing.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
