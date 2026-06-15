namespace Transfers.Dashboard.Api.Auth;

/// <summary>Bound from the "Jwt" configuration section.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "transfers-dashboard";
    public string Audience { get; set; } = "transfers-dashboard-ui";

    /// <summary>HMAC signing key. MUST be overridden via configuration/secret in production.</summary>
    public string SigningKey { get; set; } = "dev-only-super-secret-signing-key-change-me-please-32+chars";

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>Name of the HttpOnly cookie carrying the refresh token.</summary>
    public string RefreshCookieName { get; set; } = "tfx_refresh";
}
