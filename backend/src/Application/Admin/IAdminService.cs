using Universal.Transfers.Application.Admin.DTOs;

namespace Universal.Transfers.Application.Admin;

public interface IAdminService
{
    // Users
    Task<List<UserListItemDto>> GetAllUsersAsync(CancellationToken ct = default);
    Task<UserDetailDto?> GetUserByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserDetailDto> CreateUserAsync(UserCreateRequest request, CancellationToken ct = default);
    Task<UserDetailDto> UpdateUserAsync(Guid id, UserUpdateRequest request, CancellationToken ct = default);
    Task DeleteUserAsync(Guid id, CancellationToken ct = default);
    Task AddUserRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    Task RemoveUserRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default);

    // Roles
    Task<List<RoleListItemDto>> GetAllRolesAsync(CancellationToken ct = default);
    Task<RoleDetailDto?> GetRoleByIdAsync(Guid id, CancellationToken ct = default);
    Task<RoleDetailDto> CreateRoleAsync(RoleCreateRequest request, CancellationToken ct = default);
    Task<RoleDetailDto> UpdateRoleAsync(Guid id, RoleUpdateRequest request, CancellationToken ct = default);
    Task DeleteRoleAsync(Guid id, CancellationToken ct = default);
    Task AddRolePermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
    Task RemoveRolePermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);

    // Permissions
    Task<List<PermissionDto>> GetAllPermissionsAsync(CancellationToken ct = default);
    Task<PermissionDto?> GetPermissionByIdAsync(Guid id, CancellationToken ct = default);
    Task<PermissionDto> CreatePermissionAsync(PermissionCreateRequest request, CancellationToken ct = default);
    Task<PermissionDto> UpdatePermissionAsync(Guid id, PermissionUpdateRequest request, CancellationToken ct = default);
    Task DeletePermissionAsync(Guid id, CancellationToken ct = default);
}
