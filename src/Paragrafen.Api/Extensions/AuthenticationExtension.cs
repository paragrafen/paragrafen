using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using Paragrafen.Core.Options;

namespace Paragrafen.Api.Extensions;

public static class AuthenticationExtensions
{
    private const int MinimumKeyBytes = 32; // hmac-sha256 floor

    public static IServiceCollection AddJwtAuth(this IServiceCollection services,
        IConfiguration configuration, IWebHostEnvironment environment)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException($"The '{JwtOptions.SectionName}' configuration section is missing.");

        Validate(jwt);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = !environment.IsDevelopment();

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireExpirationTime = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt.IssuerSigningKey)),
                    ClockSkew = TimeSpan.Zero
                };
            });
        services.AddAuthorization();

        return services;
    }


    // Private helper for validating jwt token
    private static void Validate(JwtOptions jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt.Issuer))
        {
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:{nameof(JwtOptions.Issuer)} is missing. "
                    + "Set it in appsettings.json."
            );
        }

        if (string.IsNullOrWhiteSpace(jwt.Audience))
        {
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:{nameof(JwtOptions.Audience)} is missing. "
                    + "Set it in appsettings.json."
            );
        }

        if (jwt.AccessTokenExpirationInMinutes <= 0)
        {
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:{nameof(JwtOptions.AccessTokenExpirationInMinutes)} must be "
                    + $"greater than zero, but was {jwt.AccessTokenExpirationInMinutes}."
            );
        }

        if (string.IsNullOrWhiteSpace(jwt.IssuerSigningKey))
        {
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:{nameof(JwtOptions.IssuerSigningKey)} is missing. "
                    + "Generate one and store it in user secrets:\n"
                    + "  dotnet user-secrets set \"Jwt:IssuerSigningKey\" \"$(openssl rand -hex 48)\" "
                    + "--project src/Paragrafen.Api"
            );
        }

        var keyBytes = Encoding.UTF8.GetByteCount(jwt.IssuerSigningKey);
        if (keyBytes < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:{nameof(JwtOptions.IssuerSigningKey)} is "
                    + $"{keyBytes} bytes; HMAC-SHA256 requires at least {MinimumKeyBytes}. "
                    + "A shorter key throws IDX10653 when the first token is signed."
            );
        }
    }
}
