namespace Paragrafen.Core.Contracts.Authentication;

public sealed record AuthResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    UserResponse User);
