namespace Universal.Transfers.Application.Auth;

public interface IPermissionService
{
    Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default);
    Task<List<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default);
}
