using Microsoft.EntityFrameworkCore;
using Transfers.Dashboard.DataAccess.Context;

namespace Transfers.Dashboard.DataAccess.Repositories.Implementations;

public sealed class PermissionRepository(DashboardDbContext db) : IPermissionRepository
{
    public Task<List<string>> GetRolePermissionCodesAsync(Guid userId, CancellationToken ct = default) =>
        db.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync(ct);

    public Task<List<string>> GetDirectPermissionCodesAsync(Guid userId, CancellationToken ct = default) =>
        db.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission.Code)
            .Distinct()
            .ToListAsync(ct);

    public Task<List<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct = default) =>
        db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);
}
