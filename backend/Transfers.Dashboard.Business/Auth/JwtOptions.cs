namespace Transfers.Dashboard.Business.Auth;

/// <summary>Bound from the "Jwt" configuration section.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "transfers-dashboard";
    public string Audience { get; set; } = "transfers-dashboard-ui";
    public string SigningKey { get; set; } = "dev-only-super-secret-signing-key-change-me-please-32+chars";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
    public string RefreshCookieName { get; set; } = "tfx_refresh";
}

/// <summary>Custom claim type carrying a single permission string.</summary>
public static class CustomClaims
{
    public const string Permission = "perm";
}
