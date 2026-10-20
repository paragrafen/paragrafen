using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Services;

namespace Paragrafen.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAskService, AskService>();

        return services;
    }
}
