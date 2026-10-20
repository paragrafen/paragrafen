namespace Paragrafen.Core.Contracts.Authentication;

public sealed record AccessToken(
    string Value,
    DateTimeOffset ExpiresAt);
