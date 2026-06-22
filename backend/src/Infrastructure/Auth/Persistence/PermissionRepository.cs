using Microsoft.EntityFrameworkCore;
using Universal.Transfers.Domain.Auth.Interfaces;
using Universal.Transfers.Infrastructure.Common.Persistence;

namespace Universal.Transfers.Infrastructure.Auth.Persistence;

public sealed class PermissionRepository(AppDbContext db) : IPermissionRepository
{
    public async Task<List<string>> GetRolePermissionCodesAsync(Guid userId, CancellationToken ct = default) =>
        await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync(ct);

    public async Task<List<string>> GetDirectPermissionCodesAsync(Guid userId, CancellationToken ct = default) =>
        await db.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission.Code)
            .ToListAsync(ct);

    public async Task<List<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct = default) =>
        await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);
}
