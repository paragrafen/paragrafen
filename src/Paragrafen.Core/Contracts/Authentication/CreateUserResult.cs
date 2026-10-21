using Paragrafen.Core.Contracts.Authentication;

namespace Paragrafen.Core.Contracts.Authentication;

public sealed record CreateUserResult(
    bool Success,
    UserResponse? User,
    IReadOnlyList<CreateUserResult.Error> Errors
)
{
    public sealed record Error(string Code, string Description);
}
