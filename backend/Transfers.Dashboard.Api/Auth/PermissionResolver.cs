using Microsoft.EntityFrameworkCore;
using Transfers.Dashboard.Api.Data;

namespace Transfers.Dashboard.Api.Auth;

/// <summary>
/// Resolves a user's effective permission set:
/// direct user permissions  ∪  permissions inherited from all assigned roles.
/// </summary>
public class PermissionResolver(DashboardDbContext db)
{
    public async Task<HashSet<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        // Permissions from roles the user is assigned to.
        var rolePerms = await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .ToListAsync(ct);

        // Direct (per-user) permission grants.
        var directPerms = await db.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission.Code)
            .ToListAsync(ct);

        return rolePerms.Concat(directPerms).ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct = default) =>
        await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);
}
