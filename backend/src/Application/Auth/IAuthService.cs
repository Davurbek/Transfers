using Universal.Transfers.Application.Auth.DTOs;

namespace Universal.Transfers.Application.Auth;

public interface IAuthService
{
    Task<LoginServiceResult> LoginAsync(string username, string password, string? ipAddress, CancellationToken ct = default);
    Task<LoginServiceResult> RefreshTokenAsync(string refreshToken, string? ipAddress, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
    Task<UserInfoResponse?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
}

public record LoginServiceResult(bool Success, LoginResponse? Response, string? RefreshToken, string? ErrorMessage);
