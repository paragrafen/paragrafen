using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Contracts;
using Paragrafen.Core.Exceptions;
using Paragrafen.Core.Prompts;


namespace Paragrafen.Core.Services;

public class AskService(IAiModelService aiModelService, AskPrompt prompt, ILogger<AskService> logger) : IAskService
{
    public async Task<AskResponse> AskAsync(AskRequest request, CancellationToken cancellationToken, Guid requestId)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            throw new BadRequestException("empty_question", "Question must not be empty.");
        if (request.Question.Length > 500)
            throw new BadRequestException("question_too_long", "Question must be at most 500 characters.");

        var conversationId = request.ConversationId ?? Guid.CreateVersion7();

        // note: db is currently empty, so no documents are retrieved
        var sources = new List<Source>();
        var documents = "";
        var (system, user) = prompt.Render(request.Question, documents);

        string aiModelAnswer = await aiModelService.CompleteAsync(system, user, cancellationToken);

        var modelStatus = ReadTag(aiModelAnswer, "status")?.ToUpperInvariant();
        var status = modelStatus switch
        {
            "ANSWERED" => AnswerStatus.Answered,
            "PARTIAL" => AnswerStatus.Answered, // note: AnswerStatus has no Partial yet
            "NOT_FOUND" => AnswerStatus.NotFound,
            "OUT_OF_SCOPE" => AnswerStatus.OutOfScope,
            _ => throw BadFormat(requestId, aiModelAnswer)
        };

        var answer = ReadTag(aiModelAnswer, "answer")
            ?? throw BadFormat(requestId, aiModelAnswer);

        // if the model answers without citing sources
        if (status == AnswerStatus.Answered && sources.Count == 0)
        {
            status = AnswerStatus.NotFound;
            answer = NotFoundAnswer; // only a suggestion for what to send to frontend, can be changed
        }

        // <limitations> is optional; becomes the last paragraph of the answer when present
        var limitations = ReadTag(aiModelAnswer, "limitations");
        if (status == AnswerStatus.Answered && !string.IsNullOrWhiteSpace(limitations))
            answer = $"{answer}\n\n{limitations}";

        logger.LogInformation(
            "Request {RequestId}: prompt {PromptVersion}, model status {ModelStatus}, returned status {Status}",
            requestId, prompt.Version, modelStatus, status);

        return new AskResponse(
            requestId,
            conversationId,
            status,
            answer,
            sources,
            DateTime.UtcNow
        );
    }

    // logs the start of the reply and returns the 502 to throw
    private BadGatewayException BadFormat(Guid requestId, string reply)
    {
        logger.LogWarning("Request {RequestId}: prompt {PromptVersion}, could not read the model's reply. Start of reply: {ReplyStart}",
            requestId, prompt.Version, reply.Length > 200 ? reply[..200] : reply);
        return new BadGatewayException("model_bad_format", "The model's answer could not be read.");
    }

    private const string NotFoundAnswer = "Jeg fant ingen lovtekst som svarer på spørsmålet.";

    private static string? ReadTag(string text, string tag)
    {
        var match = Regex.Match(text, $"<{tag}>(.*?)</{tag}>", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
