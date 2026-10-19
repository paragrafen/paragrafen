namespace Paragrafen.Core.Contracts.Authentication;


public sealed record UserResponse(
    Guid Id,
    string Email,
    string UserName,
    IReadOnlyCollection<string> Roles);
