using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Contracts;
using Paragrafen.Core.Exceptions;


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

        string aiModelAnswer = await aiModelService.CompleteAsync(request.Question, cancellationToken);

        return new AskResponse(
            requestId,
            conversationId,
            AnswerStatus.Answered,
            aiModelAnswer,
            new List<Source>(),
            DateTime.UtcNow
        );

    }
}
