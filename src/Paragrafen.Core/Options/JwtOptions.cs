namespace Paragrafen.Core.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string IssuerSigningKey { get; init; } = string.Empty;
    public int AccessTokenExpirationInMinutes { get; init; } = 60;
}
