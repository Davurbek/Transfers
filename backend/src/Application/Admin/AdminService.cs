using Universal.Transfers.Application.Admin.DTOs;
using Universal.Transfers.Domain.Auth;
using Universal.Transfers.Domain.Auth.Entities;
using Universal.Transfers.Domain.Auth.Interfaces;
using Universal.Transfers.Domain.Common.Security;

namespace Universal.Transfers.Application.Admin;

public class AdminService(IAdminRepository repo) : IAdminService
{
    public async Task<List<UserListItemDto>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var users = await repo.GetAllUsersAsync(ct);
        return users.Select(u => new UserListItemDto(
            u.Id, u.Username, u.Email, u.IsActive, u.CreatedAt,
            u.UserRoles.Select(ur => ur.Role.Name).ToList())).ToList();
    }

    public async Task<UserDetailDto?> GetUserByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await repo.GetUserWithPermissionsAsync(id, ct);
        if (user is null) return null;
        return ToUserDetail(user);
    }

    public async Task<UserDetailDto> CreateUserAsync(UserCreateRequest request, CancellationToken ct = default)
    {
        if (await repo.UsernameExistsAsync(request.Username, ct))
            throw new InvalidOperationException($"Username '{request.Username}' already exists");

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = PasswordHasher.Hash(request.Password),
            IsActive = true,
        };
        await repo.AddUserAsync(user, ct);
        await repo.SaveChangesAsync(ct);

        if (request.RoleIds is { Count: > 0 })
        {
            foreach (var roleId in request.RoleIds)
            {
                if (!await repo.UserHasRoleAsync(user.Id, roleId, ct))
                    await repo.AddUserRoleAsync(new UserRole { UserId = user.Id, RoleId = roleId }, ct);
            }
            await repo.SaveChangesAsync(ct);
        }

        var created = await repo.GetUserWithPermissionsAsync(user.Id, ct);
        return ToUserDetail(created ?? user);
    }

    public async Task<UserDetailDto> UpdateUserAsync(Guid id, UserUpdateRequest request, CancellationToken ct = default)
    {
        var user = await repo.GetUserWithPermissionsAsync(id, ct)
            ?? throw new InvalidOperationException("User not found");

        user.Username = request.Username;
        user.Email = request.Email;
        user.IsActive = request.IsActive;
        await repo.SaveChangesAsync(ct);
        return ToUserDetail(user);
    }

    public async Task DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        var user = await repo.GetUserByIdAsync(id, ct)
            ?? throw new InvalidOperationException("User not found");
        user.IsActive = false;
        await repo.SaveChangesAsync(ct);
    }

    public async Task AddUserRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        if (await repo.UserHasRoleAsync(userId, roleId, ct))
            return;
        await repo.AddUserRoleAsync(new UserRole { UserId = userId, RoleId = roleId }, ct);
        await repo.SaveChangesAsync(ct);
    }

    public async Task RemoveUserRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        await repo.RemoveUserRoleAsync(userId, roleId, ct);
        await repo.SaveChangesAsync(ct);
    }

    public async Task AddUserPermissionAsync(Guid userId, Guid permissionId, CancellationToken ct = default)
    {
        if (await repo.UserHasPermissionAsync(userId, permissionId, ct))
            return;
        await repo.AddUserPermissionAsync(new UserPermission { UserId = userId, PermissionId = permissionId }, ct);
        await repo.SaveChangesAsync(ct);
    }

    public async Task RemoveUserPermissionAsync(Guid userId, Guid permissionId, CancellationToken ct = default)
    {
        await repo.RemoveUserPermissionAsync(userId, permissionId, ct);
        await repo.SaveChangesAsync(ct);
    }

    public async Task<List<RoleListItemDto>> GetAllRolesAsync(CancellationToken ct = default)
    {
        var roles = await repo.GetAllRolesAsync(ct);
        return roles.Select(r => new RoleListItemDto(r.Id, r.Name, r.Description)).ToList();
    }

    public async Task<RoleDetailDto?> GetRoleByIdAsync(Guid id, CancellationToken ct = default)
    {
        var role = await repo.GetRoleWithPermissionsAsync(id, ct);
        if (role is null) return null;
        return new RoleDetailDto(role.Id, role.Name, role.Description,
            role.RolePermissions.Select(rp => new PermissionDto(rp.Permission.Id, rp.Permission.Code, rp.Permission.Description)).ToList());
    }

    public async Task<RoleDetailDto> CreateRoleAsync(RoleCreateRequest request, CancellationToken ct = default)
    {
        if (await repo.RoleNameExistsAsync(request.Name, ct))
            throw new InvalidOperationException($"Role '{request.Name}' already exists");

        var role = new Role { Name = request.Name, Description = request.Description };
        await repo.AddRoleAsync(role, ct);
        await repo.SaveChangesAsync(ct);
        return new RoleDetailDto(role.Id, role.Name, role.Description, []);
    }

    public async Task<RoleDetailDto> UpdateRoleAsync(Guid id, RoleUpdateRequest request, CancellationToken ct = default)
    {
        var role = await repo.GetRoleWithPermissionsAsync(id, ct)
            ?? throw new InvalidOperationException("Role not found");
        role.Name = request.Name;
        role.Description = request.Description;
        await repo.SaveChangesAsync(ct);
        return new RoleDetailDto(role.Id, role.Name, role.Description,
            role.RolePermissions.Select(rp => new PermissionDto(rp.Permission.Id, rp.Permission.Code, rp.Permission.Description)).ToList());
    }

    public async Task DeleteRoleAsync(Guid id, CancellationToken ct = default)
    {
        await repo.DeleteRoleAsync(id, ct);
        await repo.SaveChangesAsync(ct);
    }

    public async Task AddRolePermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default)
    {
        if (await repo.RoleHasPermissionAsync(roleId, permissionId, ct))
            return;
        await repo.AddRolePermissionAsync(new RolePermission { RoleId = roleId, PermissionId = permissionId }, ct);
        await repo.SaveChangesAsync(ct);
    }

    public async Task RemoveRolePermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default)
    {
        await repo.RemoveRolePermissionAsync(roleId, permissionId, ct);
        await repo.SaveChangesAsync(ct);
    }

    public async Task<List<PermissionDto>> GetAllPermissionsAsync(CancellationToken ct = default)
    {
        var perms = await repo.GetAllPermissionsAsync(ct);
        return perms.Select(p => new PermissionDto(p.Id, p.Code, p.Description)).ToList();
    }

    public async Task<PermissionDto?> GetPermissionByIdAsync(Guid id, CancellationToken ct = default)
    {
        var perm = await repo.GetPermissionByIdAsync(id, ct);
        return perm is null ? null : new PermissionDto(perm.Id, perm.Code, perm.Description);
    }

    public async Task<PermissionDto> CreatePermissionAsync(PermissionCreateRequest request, CancellationToken ct = default)
    {
        if (await repo.PermissionCodeExistsAsync(request.Code, ct))
            throw new InvalidOperationException($"Permission code '{request.Code}' already exists");

        var perm = new Permission { Code = request.Code, Description = request.Description };
        await repo.AddPermissionAsync(perm, ct);
        await repo.SaveChangesAsync(ct);
        return new PermissionDto(perm.Id, perm.Code, perm.Description);
    }

    public async Task<PermissionDto> UpdatePermissionAsync(Guid id, PermissionUpdateRequest request, CancellationToken ct = default)
    {
        var perm = await repo.GetPermissionByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Permission not found");
        perm.Code = request.Code;
        perm.Description = request.Description;
        await repo.SaveChangesAsync(ct);
        return new PermissionDto(perm.Id, perm.Code, perm.Description);
    }

    public async Task DeletePermissionAsync(Guid id, CancellationToken ct = default)
    {
        await repo.DeletePermissionAsync(id, ct);
        await repo.SaveChangesAsync(ct);
    }

    private static UserDetailDto ToUserDetail(User user) => new(
        user.Id, user.Username, user.Email, user.IsActive, user.CreatedAt,
        user.UserRoles.Select(ur => new RoleDto(ur.Role.Id, ur.Role.Name, ur.Role.Description)).ToList(),
        user.UserPermissions.Select(up => new PermissionDto(up.Permission.Id, up.Permission.Code, up.Permission.Description)).ToList());
}
