using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Options;
using Paragrafen.Data.Ai;
using Paragrafen.Data.Identity;
using Paragrafen.Data.Persistence;

namespace Paragrafen.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddData(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = configuration.GetConnectionString("Paragrafen");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Paragrafen is missing. Locally, store it in user secrets; "
                + "elsewhere, set the ConnectionStrings__Paragrafen environment variable."
            );
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        if (string.IsNullOrEmpty(dataSourceBuilder.ConnectionStringBuilder.Password))
        {
            // Azure: no password, log in with the app's managed identity instead.
            var credential = new DefaultAzureCredential();
            dataSourceBuilder.UsePeriodicPasswordProvider(
                async (_, cancellationToken) =>
                    (await credential.GetTokenAsync(
                        new TokenRequestContext(["https://ossrdbms-aad.database.windows.net/.default"]),
                        cancellationToken)).Token,
                TimeSpan.FromMinutes(55),
                TimeSpan.FromSeconds(5));
        }
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContext<ParagrafenDbContext>(options => options.UseNpgsql(dataSource));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        // AddIdentityCore, NOT AddIdentity: AddIdentity also registers cookie
        // authentication and overwrites DefaultAuthenticateScheme, which turns every
        // protected endpoint into a 302 to /Account/Login instead of a 401.
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // Defaults to false, and login looks users up by email.
                options.User.RequireUniqueEmail = true;

                // RegisterRequestValidator is the authority on the password policy;
                // these are relaxed so Identity never produces a competing message.
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ParagrafenDbContext>();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        //services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

        services.AddSingleton(_ =>
        {
            var endpoint = configuration["Foundry:Endpoint"]
                           ?? throw new InvalidOperationException("Foundry:Endpoint is not configured.");
            var chatDeployment = configuration["Foundry:ChatClient"]
                                 ?? throw new InvalidOperationException("Foundry:ChatClient is not configured.");
            var client = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
            return client.GetChatClient(chatDeployment);
        });
        services.AddSingleton<IAiModelService, AiModelService>();

        return services;
    }
}
