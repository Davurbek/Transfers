using Transfers.Dashboard.Domain.Entities.Auth;

namespace Transfers.Dashboard.DataAccess.Repositories;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
