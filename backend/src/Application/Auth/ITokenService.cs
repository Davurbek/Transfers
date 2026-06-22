using Universal.Transfers.Domain.Auth.Entities;

namespace Universal.Transfers.Application.Auth;

public interface ITokenService
{
    string GenerateAccessToken(User user, IList<string> permissions, IList<string> roles);
    string GenerateRefreshToken();
    string HashToken(string token);
}
