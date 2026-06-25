using Microsoft.EntityFrameworkCore;
using Universal.Transfers.Domain.Auth.Entities;
using Universal.Transfers.Domain.Auth.Interfaces;
using Universal.Transfers.Infrastructure.Common.Persistence;

namespace Universal.Transfers.Infrastructure.Auth.Persistence;

public sealed class AdminRepository(AppDbContext db) : IAdminRepository
{
    public async Task<List<User>> GetAllUsersAsync(CancellationToken ct = default) =>
        await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).OrderBy(u => u.Username).ToListAsync(ct);

    public Task<User?> GetUserByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetUserWithRolesAsync(Guid id, CancellationToken ct = default) =>
        db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.Username == username, ct);

    public async Task AddUserAsync(User user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);

    public async Task<List<Role>> GetAllRolesAsync(CancellationToken ct = default) =>
        await db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).OrderBy(r => r.Name).ToListAsync(ct);

    public Task<Role?> GetRoleWithPermissionsAsync(Guid id, CancellationToken ct = default) =>
        db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> RoleNameExistsAsync(string name, CancellationToken ct = default) =>
        db.Roles.AnyAsync(r => r.Name == name, ct);

    public async Task AddRoleAsync(Role role, CancellationToken ct = default) =>
        await db.Roles.AddAsync(role, ct);

    public async Task DeleteRoleAsync(Guid id, CancellationToken ct = default)
    {
        var role = await db.Roles.Include(r => r.UserRoles).Include(r => r.RolePermissions).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is not null)
        {
            db.RolePermissions.RemoveRange(role.RolePermissions);
            db.UserRoles.RemoveRange(role.UserRoles);
            db.Roles.Remove(role);
        }
    }

    public async Task<List<Permission>> GetAllPermissionsAsync(CancellationToken ct = default) =>
        await db.Permissions.OrderBy(p => p.Code).ToListAsync(ct);

    public Task<Permission?> GetPermissionByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Permissions.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> PermissionCodeExistsAsync(string code, CancellationToken ct = default) =>
        db.Permissions.AnyAsync(p => p.Code == code, ct);

    public async Task AddPermissionAsync(Permission permission, CancellationToken ct = default) =>
        await db.Permissions.AddAsync(permission, ct);

    public async Task DeletePermissionAsync(Guid id, CancellationToken ct = default)
    {
        var perm = await db.Permissions.Include(p => p.RolePermissions).Include(p => p.UserPermissions).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (perm is not null)
        {
            db.RolePermissions.RemoveRange(perm.RolePermissions);
            db.UserPermissions.RemoveRange(perm.UserPermissions);
            db.Permissions.Remove(perm);
        }
    }

    public async Task AddUserRoleAsync(UserRole userRole, CancellationToken ct = default) =>
        await db.UserRoles.AddAsync(userRole, ct);

    public async Task RemoveUserRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        var ur = await db.UserRoles.FirstOrDefaultAsync(x => x.UserId == userId && x.RoleId == roleId, ct);
        if (ur is not null) db.UserRoles.Remove(ur);
    }

    public Task<bool> UserHasRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default) =>
        db.UserRoles.AnyAsync(x => x.UserId == userId && x.RoleId == roleId, ct);

    public async Task AddRolePermissionAsync(RolePermission rolePermission, CancellationToken ct = default) =>
        await db.RolePermissions.AddAsync(rolePermission, ct);

    public async Task RemoveRolePermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default)
    {
        var rp = await db.RolePermissions.FirstOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, ct);
        if (rp is not null) db.RolePermissions.Remove(rp);
    }

    public Task<bool> RoleHasPermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default) =>
        db.RolePermissions.AnyAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, ct);
}
