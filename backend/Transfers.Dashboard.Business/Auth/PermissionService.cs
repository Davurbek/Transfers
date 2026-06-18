using Transfers.Dashboard.DataAccess.Repositories;

namespace Transfers.Dashboard.Business.Auth;

public sealed class PermissionService(IPermissionRepository permissions) : IPermissionService
{
    public async Task<HashSet<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        var rolePerms = await permissions.GetRolePermissionCodesAsync(userId, ct);
        var directPerms = await permissions.GetDirectPermissionCodesAsync(userId, ct);
        return rolePerms.Concat(directPerms).ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct = default) =>
        await permissions.GetRoleNamesAsync(userId, ct);
}
