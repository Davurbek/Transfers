using Universal.Transfers.Domain.Auth.Entities;

namespace Universal.Transfers.Domain.Auth.Interfaces;

public interface IAdminRepository
{
    // Users
    Task<List<User>> GetAllUsersAsync(CancellationToken ct = default);
    Task<User?> GetUserByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetUserWithRolesAsync(Guid id, CancellationToken ct = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default);
    Task AddUserAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    // Roles
    Task<List<Role>> GetAllRolesAsync(CancellationToken ct = default);
    Task<Role?> GetRoleWithPermissionsAsync(Guid id, CancellationToken ct = default);
    Task<bool> RoleNameExistsAsync(string name, CancellationToken ct = default);
    Task AddRoleAsync(Role role, CancellationToken ct = default);
    Task DeleteRoleAsync(Guid id, CancellationToken ct = default);

    // Permissions
    Task<List<Permission>> GetAllPermissionsAsync(CancellationToken ct = default);
    Task<Permission?> GetPermissionByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> PermissionCodeExistsAsync(string code, CancellationToken ct = default);
    Task AddPermissionAsync(Permission permission, CancellationToken ct = default);
    Task DeletePermissionAsync(Guid id, CancellationToken ct = default);

    // User-Role
    Task AddUserRoleAsync(UserRole userRole, CancellationToken ct = default);
    Task RemoveUserRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    Task<bool> UserHasRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default);

    // User-Permission
    Task<User?> GetUserWithPermissionsAsync(Guid id, CancellationToken ct = default);
    Task AddUserPermissionAsync(UserPermission userPermission, CancellationToken ct = default);
    Task RemoveUserPermissionAsync(Guid userId, Guid permissionId, CancellationToken ct = default);
    Task<bool> UserHasPermissionAsync(Guid userId, Guid permissionId, CancellationToken ct = default);

    // Role-Permission
    Task AddRolePermissionAsync(RolePermission rolePermission, CancellationToken ct = default);
    Task RemoveRolePermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
    Task<bool> RoleHasPermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
}
