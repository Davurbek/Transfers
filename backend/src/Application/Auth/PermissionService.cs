using Universal.Transfers.Domain.Auth.Interfaces;

namespace Universal.Transfers.Application.Auth;

public class PermissionService(
    IPermissionRepository permissionRepo) : IPermissionService
{
    public async Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        var rolePerms = await permissionRepo.GetRolePermissionCodesAsync(userId, ct);
        var directPerms = await permissionRepo.GetDirectPermissionCodesAsync(userId, ct);
        return rolePerms.Union(directPerms).Distinct().ToList();
    }

    public async Task<List<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default) =>
        await permissionRepo.GetRoleNamesAsync(userId, ct);
}
