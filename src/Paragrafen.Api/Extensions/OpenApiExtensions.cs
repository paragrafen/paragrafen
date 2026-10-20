using Microsoft.OpenApi;

namespace Paragrafen.Api.Extensions;

public static class OpenApiExtensions
{
    private const string SchemeName = "Bearer";

    private const string DocumentPath = "/openapi/v1.json";

    private const string DocumentTitle = "Paragrafen API v1";

    /// <summary>
    /// Generates the OpenAPI document and declares the bearer scheme, so Swagger UI
    /// can render an Authorize button and attach the token to every request.
    /// </summary>
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer(
                (document, _, _) =>
                {
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes ??=
                        new Dictionary<string, IOpenApiSecurityScheme>();

                    document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer", // lowercase; some tooling is case-sensitive
                        BearerFormat = "JWT",
                        In = ParameterLocation.Header,
                    };

                    // Marks every operation as secured, the anonymous ones included.
                    // Cosmetic only: AllowAnonymous still lets them through.
                    document.Security =
                    [
                        new OpenApiSecurityRequirement
                        {
                            [new OpenApiSecuritySchemeReference(SchemeName, document)] = [],
                        },
                    ];

                    return Task.CompletedTask;
                }
            );
        });

        return services;
    }

    /// <summary>
    /// Serves Swagger UI at /swagger against the document produced by AddOpenApi.
    /// Only the UI package is referenced — the document itself comes from the
    /// built-in generator, not from SwaggerGen.
    /// </summary>
    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        app.MapOpenApi();
        app.UseSwaggerUI(options => options.SwaggerEndpoint(DocumentPath, DocumentTitle));

        return app;
    }
}
