namespace Paragrafen.Api.Extensions;

public static class CorsExtensions
{
    // Origins come from the AllowedOrigins setting; none configured means no cross-origin access.
    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
            options.AddDefaultPolicy(policy =>
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()));

        return services;
    }
}
