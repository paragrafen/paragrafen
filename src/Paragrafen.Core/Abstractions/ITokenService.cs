using Paragrafen.Core.Contracts;
using Paragrafen.Core.Contracts.Authentication;

namespace Paragrafen.Core.Abstractions;

public interface ITokenService
{
    AccessToken CreateAccessToken(UserResponse userAccount);
}
