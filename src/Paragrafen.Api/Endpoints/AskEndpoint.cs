using Microsoft.AspNetCore.Http.HttpResults;
using Paragrafen.Core.Contracts;
using Paragrafen.Core.Abstractions;

namespace Paragrafen.Api.Endpoints;

public static class AskEndpoints

{
    public static IEndpointRouteBuilder MapAskEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/ask", Ask)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError)
            .Produces<ErrorResponse>(StatusCodes.Status502BadGateway)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<Ok<AskResponse>> Ask(
        AskRequest request,
        IAskService askService,
        CancellationToken cancellationToken,
        HttpContext context
        )
    {
        var requestId = context.GetRequestId();
        var response = await askService.AskAsync(request, cancellationToken, requestId);

        return TypedResults.Ok(response);
    }

}
