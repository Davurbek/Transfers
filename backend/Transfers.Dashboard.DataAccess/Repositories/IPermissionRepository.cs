namespace Transfers.Dashboard.DataAccess.Repositories;

public interface IPermissionRepository
{
    /// <summary>Permission codes granted to the user through any assigned role.</summary>
    Task<List<string>> GetRolePermissionCodesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Permission codes granted directly to the user.</summary>
    Task<List<string>> GetDirectPermissionCodesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Names of the roles assigned to the user.</summary>
    Task<List<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct = default);
}
