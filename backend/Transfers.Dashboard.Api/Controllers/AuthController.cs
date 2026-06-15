using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Transfers.Dashboard.Api.Auth;
using Transfers.Dashboard.Api.Data;
using Transfers.Dashboard.Api.Dtos;
using Transfers.Dashboard.Api.Services;

namespace Transfers.Dashboard.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    DashboardDbContext db,
    TokenService tokens,
    PermissionResolver resolver,
    IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    /// <summary>Authenticate with username/password. Sets the refresh-token cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username, ct);
        if (user is null || !user.IsActive || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid credentials" });

        var access = await tokens.CreateAccessTokenAsync(user, ct);
        var refresh = await tokens.CreateRefreshTokenAsync(user, GetIp(), ct);
        SetRefreshCookie(refresh.RawValue, refresh.ExpiresAt);

        return Ok(await BuildAuthResponseAsync(user.Id, user.Username, user.Email, access, ct));
    }

    /// <summary>Exchange a valid refresh-token cookie for a new access token (and rotate the refresh token).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(_jwt.RefreshCookieName, out var raw) || string.IsNullOrEmpty(raw))
            return Unauthorized(new { message = "Missing refresh token" });

        var result = await tokens.RotateRefreshTokenAsync(raw, GetIp(), ct);
        if (result is null)
        {
            ClearRefreshCookie();
            return Unauthorized(new { message = "Invalid or expired refresh token" });
        }

        var (user, rotated) = result.Value;
        SetRefreshCookie(rotated.RawValue, rotated.ExpiresAt);
        var access = await tokens.CreateAccessTokenAsync(user, ct);
        return Ok(await BuildAuthResponseAsync(user.Id, user.Username, user.Email, access, ct));
    }

    /// <summary>Revoke the current refresh token and clear the cookie.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(_jwt.RefreshCookieName, out var raw) && !string.IsNullOrEmpty(raw))
            await tokens.RevokeAsync(raw, ct);
        ClearRefreshCookie();
        return NoContent();
    }

    /// <summary>Return the authenticated user's identity and effective permissions.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserInfo>> Me(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Unauthorized();

        var roles = await resolver.GetRoleNamesAsync(userId, ct);
        var perms = await resolver.GetEffectivePermissionsAsync(userId, ct);
        return Ok(new UserInfo(user.Id, user.Username, user.Email, roles, perms.OrderBy(p => p).ToList()));
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(
        Guid userId, string username, string email, AccessToken access, CancellationToken ct)
    {
        var roles = await resolver.GetRoleNamesAsync(userId, ct);
        var perms = await resolver.GetEffectivePermissionsAsync(userId, ct);
        var info = new UserInfo(userId, username, email, roles, perms.OrderBy(p => p).ToList());
        return new AuthResponse(access.Value, access.ExpiresAt, info);
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
