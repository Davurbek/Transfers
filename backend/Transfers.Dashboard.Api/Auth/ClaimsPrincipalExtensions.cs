using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Transfers.Dashboard.Api.Authorization;

namespace Transfers.Dashboard.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    public static string GetUsername(this ClaimsPrincipal user) =>
        user.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
        ?? user.Identity?.Name
        ?? "unknown";

    public static IReadOnlyList<string> GetPermissions(this ClaimsPrincipal user) =>
        user.FindAll(CustomClaims.Permission).Select(c => c.Value).ToList();
}
