namespace Transfers.Dashboard.Business.Auth;

public interface IPermissionService
{
    /// <summary>
    /// Effective permissions = direct user permissions ∪ permissions inherited
    /// from all assigned roles.
    /// </summary>
    Task<HashSet<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct = default);
}
