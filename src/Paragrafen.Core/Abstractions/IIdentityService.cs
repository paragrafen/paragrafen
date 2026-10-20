using Paragrafen.Core.Contracts;
using Paragrafen.Core.Contracts.Authentication;

namespace Paragrafen.Core.Abstractions;

public interface IIdentityService
{
    Task<UserResponse?> FindByEmailAsync(
        string email, CancellationToken cancellationToken);

    Task<CreateUserResult> CreateUserAsync(
        RegisterRequest registerRequest, CancellationToken cancellationToken);

    Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken cancellationToken);
}
