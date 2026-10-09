using Microsoft.AspNetCore.Builder;

namespace Paragrafen.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => new { status = "ok" });

        return app;
    }

}


