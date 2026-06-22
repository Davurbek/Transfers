using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Universal.Transfers.Application.Auth;
using Universal.Transfers.Application.Auth.DTOs;

namespace Universal.Transfers.Api.Auth;

[ApiController]
[Route("auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await authService.LoginAsync(request.Username, request.Password, ip, ct);
        if (!result.Success)
            return Unauthorized(new { message = result.ErrorMessage });

        SetRefreshCookie(result.RefreshToken!);
        return Ok(result.Response);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var raw = Request.Cookies["tfx_refresh"];
        if (string.IsNullOrWhiteSpace(raw))
            return Unauthorized(new { message = "No refresh token" });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await authService.RefreshTokenAsync(raw, ip, ct);
        if (!result.Success)
            return Unauthorized(new { message = result.ErrorMessage });

        SetRefreshCookie(result.RefreshToken!);
        return Ok(result.Response);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var raw = Request.Cookies["tfx_refresh"];
        if (!string.IsNullOrWhiteSpace(raw))
            await authService.LogoutAsync(raw, ct);

        Response.Cookies.Delete("tfx_refresh");
        return Ok(new { message = "Logged out" });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var userId = HttpContext.User.GetUserId();
        var user = await authService.GetCurrentUserAsync(userId, ct);
        if (user is null) return NotFound();
        return Ok(user);
    }

    private void SetRefreshCookie(string token)
    {
        Response.Cookies.Append("tfx_refresh", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(7),
        });
    }
}
