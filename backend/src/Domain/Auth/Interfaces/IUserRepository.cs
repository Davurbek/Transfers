using Universal.Transfers.Domain.Auth.Entities;

namespace Universal.Transfers.Domain.Auth.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
