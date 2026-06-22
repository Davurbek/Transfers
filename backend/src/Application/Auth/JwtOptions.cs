namespace Universal.Transfers.Application.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; }
    public int RefreshTokenDays { get; set; }
    public string RefreshCookieName { get; set; } = string.Empty;
}

public static class CustomClaims
{
    public const string Permission = "perm";
}
