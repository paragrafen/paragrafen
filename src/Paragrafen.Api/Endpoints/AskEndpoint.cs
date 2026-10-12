using Microsoft.AspNetCore.Http.HttpResults;
using Paragrafen.Core.Contracts;
using Paragrafen.Core.Exceptions;

namespace Paragrafen.Api.Endpoints;

public static class AskEndpoints
{
    public static IEndpointRouteBuilder MapAskEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/ask", Ask);

        return app;
    }

    private static Ok<AskResponse> Ask(AskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            throw new BadRequestException("empty_question", "Question must not be empty.");
        if (request.Question.Length > 500)
            throw new BadRequestException("question_too_long", "Question must be at most 500 characters.");

        var requestId = Guid.CreateVersion7();
        var conversationId = request.ConversationId ?? Guid.CreateVersion7();

        var response = new AskResponse(
            requestId,
            conversationId,
            AnswerStatus.Answered,
            request.Question,
            new List<Source>(),
            DateTime.UtcNow
            );

        return TypedResults.Ok(response);
    }

}
