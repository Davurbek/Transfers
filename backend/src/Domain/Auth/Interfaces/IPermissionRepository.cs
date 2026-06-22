namespace Universal.Transfers.Domain.Auth.Interfaces;

public interface IPermissionRepository
{
    Task<List<string>> GetRolePermissionCodesAsync(Guid userId, CancellationToken ct = default);
    Task<List<string>> GetDirectPermissionCodesAsync(Guid userId, CancellationToken ct = default);
    Task<List<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct = default);
}
