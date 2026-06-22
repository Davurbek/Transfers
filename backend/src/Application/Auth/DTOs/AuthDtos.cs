namespace Universal.Transfers.Application.Auth.DTOs;

public record LoginRequest(string Username, string Password);
public record RefreshRequest(string RefreshToken);
public record LoginResponse(string AccessToken, DateTime AccessTokenExpiresAt, UserInfoResponse User);
public record UserInfoResponse(Guid Id, string Username, string Email, List<string> Permissions, List<string> Roles);
