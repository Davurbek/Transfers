namespace Transfers.Dashboard.Business.Dtos;

public record LoginRequest(string Username, string Password);

public record UserInfo(
    Guid Id,
    string Username,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

/// <summary>Internal result of an auth operation, including the raw refresh token.</summary>
public record AuthResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserInfo User);

/// <summary>What the API returns to the client (refresh token travels via cookie, not body).</summary>
public record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    UserInfo User);
