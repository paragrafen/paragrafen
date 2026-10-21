using Microsoft.AspNetCore.Http.HttpResults;
using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Authorization;
using Paragrafen.Core.Contracts;
using Paragrafen.Core.Contracts.Results;

namespace Paragrafen.Api.Endpoints;

public static class ResultsEndpoints
{
    public static IEndpointRouteBuilder MapResultsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/results", GetResults)
            .WithTags("Results")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<Ok<ResultsResponse>> GetResults(
        IResultsReader resultsReader,
        CancellationToken cancellationToken)
    {
        var response = await resultsReader.ReadAsync(cancellationToken);
        return TypedResults.Ok(response);
    }
}
