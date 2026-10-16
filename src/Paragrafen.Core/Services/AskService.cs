using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Contracts;
using Paragrafen.Core.Exceptions;
using Paragrafen.Core.Models;


namespace Paragrafen.Core.Services;

public class AskService(IAiModelService aiModelService) : IAskService
{
    public async Task<AskResponse> AskAsync(AskRequest request, CancellationToken cancellationToken, Guid requestId)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            throw new BadRequestException("empty_question", "Question must not be empty.");
        if (request.Question.Length > 500)
            throw new BadRequestException("question_too_long", "Question must be at most 500 characters.");

        var conversationId = request.ConversationId ?? Guid.CreateVersion7();

        ModelResult result = await aiModelService.CompleteAsync(request.Question, cancellationToken);

        if (string.IsNullOrWhiteSpace(result.Text))
            throw new BadGatewayException("model_no_answer", "The model did not return anything");

        return new AskResponse(
            requestId,
            conversationId,
            AnswerStatus.Answered,
            result.Text,
            new List<Source>(),
            DateTime.UtcNow
        );

    }
}
