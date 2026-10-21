namespace Paragrafen.Core.Contracts.Authentication;

public sealed record RegisterRequest(
    string Email,
    string UserName,
    string Password);
