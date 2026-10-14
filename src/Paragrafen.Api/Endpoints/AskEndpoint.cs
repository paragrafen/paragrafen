using Microsoft.AspNetCore.Http.HttpResults;
using Paragrafen.Core.Contracts;
using Paragrafen.Core.Exceptions;
using OpenAI.Chat;
using Paragrafen.Core.Abstractions;

namespace Paragrafen.Api.Endpoints;

public static class AskEndpoints

{
    public static IEndpointRouteBuilder MapAskEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/ask", Ask)
            .ProducesProblem(400).ProducesProblem(502).ProducesProblem(503);

        return app;
    }

    private static async Task<Ok<AskResponse>> Ask(
        AskRequest request,
        IAskService askService,
        CancellationToken cancellationToken
        )
    {
        var response = await askService.AskAsync(request, cancellationToken);

        return TypedResults.Ok(response);
    }

}
