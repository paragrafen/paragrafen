using Paragrafen.Core.Contracts;

namespace Paragrafen.Api.Endpoints;

public static class AskEndpoints
{
    public static IEndpointRouteBuilder MapAskEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ask", Ask);

        return app;
    }

    private static AskResponse Ask(AskRequest request)
    {
        return new AskResponse(
            Guid.Empty,
            Guid.Empty,
            AnswerStatus.Answered,
            $"{request.Question}",
            new List<Source>(),
            DateTime.UtcNow
            );
    }

}
