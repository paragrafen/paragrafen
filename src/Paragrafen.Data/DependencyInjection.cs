using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Models;
using Paragrafen.Core.Options;
using Paragrafen.Data.Ai;
using Paragrafen.Data.Calls;
using Paragrafen.Data.Identity;
using Paragrafen.Data.Persistence;
using Paragrafen.Data.Search;

using Paragrafen.Core.Prompts;
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

        services.AddSingleton(_ => ParagrafenDataSource.Create(connectionString, new DefaultAzureCredential()));
        services.AddDbContext<ParagrafenDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()));

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

        var endpoint = configuration["Foundry:Endpoint"]
                       ?? throw new InvalidOperationException("Foundry:Endpoint is not configured.");
        var chatDeployment = configuration["Foundry:ChatClient"]
                             ?? throw new InvalidOperationException("Foundry:ChatClient is not configured.");
        var embeddingDeployment = configuration["Foundry:EmbeddingClient"]
                                  ?? throw new InvalidOperationException("Foundry:EmbeddingClient is not configured.");

        services.AddSingleton(_ => new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential()));
        services.AddSingleton(sp => sp.GetRequiredService<AzureOpenAIClient>().GetChatClient(chatDeployment));
        services.AddSingleton(sp => sp.GetRequiredService<AzureOpenAIClient>().GetEmbeddingClient(embeddingDeployment));

        services.AddSingleton<IAiModelService, AiModelService>();
        services.AddSingleton<IEmbeddingService, EmbeddingService>();


        services.AddSingleton(_ =>
        {
            var deployment = configuration["Foundry:ChatClient"]
                             ?? throw new InvalidOperationException("Foundry:ChatClient is not configured.");
            return configuration.GetSection($"Foundry:Prices:{deployment}").Get<ModelPrice>()
                   ?? throw new InvalidOperationException($"Foundry:Prices:{deployment} is not configured.");
        });
        services.AddSingleton(AskPrompt.Load("v0"));
        services.AddSingleton<ICallLog, CallLog>();
        services.AddSingleton<IChunkSearch, ChunkSearch>();



        return services;
    }
}
