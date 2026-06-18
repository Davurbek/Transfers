using Transfers.Dashboard.Domain.Entities.Auth;

namespace Transfers.Dashboard.Business.Auth;

public record AccessToken(string Value, DateTimeOffset ExpiresAt);
public record IssuedRefreshToken(string RawValue, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    Task<AccessToken> CreateAccessTokenAsync(User user, CancellationToken ct = default);
    Task<IssuedRefreshToken> CreateRefreshTokenAsync(User user, string? ip, CancellationToken ct = default);

    /// <summary>Validates a raw refresh token, rotates it, and returns the user + new token.</summary>
    Task<(User user, IssuedRefreshToken rotated)?> RotateRefreshTokenAsync(
        string rawToken, string? ip, CancellationToken ct = default);

    /// <summary>Revokes a refresh token (logout / session kill). Idempotent.</summary>
    Task RevokeAsync(string rawToken, CancellationToken ct = default);
}
