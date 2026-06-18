using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Transfers.Dashboard.Api.Auth;
using Transfers.Dashboard.Business.Auth;
using Transfers.Dashboard.Business.Dtos;
using Transfers.Dashboard.Business.Services;

namespace Transfers.Dashboard.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    /// <summary>Authenticate with username/password. Sets the refresh-token cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request.Username, request.Password, GetIp(), ct);
        if (result is null)
            return Unauthorized(new { message = "Invalid credentials" });

        return Ok(ToResponse(result));
    }

    /// <summary>Exchange a valid refresh-token cookie for a new access token (rotates the refresh token).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(_jwt.RefreshCookieName, out var raw) || string.IsNullOrEmpty(raw))
            return Unauthorized(new { message = "Missing refresh token" });

        var result = await authService.RefreshAsync(raw, GetIp(), ct);
        if (result is null)
        {
            ClearRefreshCookie();
            return Unauthorized(new { message = "Invalid or expired refresh token" });
        }

        return Ok(ToResponse(result));
    }

    /// <summary>Revoke the current refresh token and clear the cookie.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(_jwt.RefreshCookieName, out var raw) && !string.IsNullOrEmpty(raw))
            await authService.LogoutAsync(raw, ct);
        ClearRefreshCookie();
        return NoContent();
    }

    /// <summary>Return the authenticated user's identity and effective permissions.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserInfo>> Me(CancellationToken ct)
    {
        var info = await authService.GetCurrentUserAsync(User.GetUserId(), ct);
        return info is null ? Unauthorized() : Ok(info);
    }

    private AuthResponse ToResponse(AuthResult result)
    {
        SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
        return new AuthResponse(result.AccessToken, result.AccessTokenExpiresAt, result.User);
    }

    private string GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private void SetRefreshCookie(string value, DateTimeOffset expires) =>
        Response.Cookies.Append(_jwt.RefreshCookieName, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expires,
            Path = "/api/auth",
        });

    private void ClearRefreshCookie() =>
        Response.Cookies.Delete(_jwt.RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
        });
}
