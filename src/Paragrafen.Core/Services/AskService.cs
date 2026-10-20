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

        var status = ReadTag(aiModelAnswer, "status")?.ToUpperInvariant() switch
        {
            "ANSWERED" => AnswerStatus.Answered,
            "PARTIAL" => AnswerStatus.Answered, // note: AnswerStatus has no Partial yet
            "NOT_FOUND" => AnswerStatus.NotFound,
            "OUT_OF_SCOPE" => AnswerStatus.OutOfScope,
            _ => throw BadFormat(requestId, aiModelAnswer)
        };

        var answer = ReadTag(aiModelAnswer, "answer")
            ?? throw BadFormat(requestId, aiModelAnswer);

        return new AskResponse(
            requestId,
            conversationId,
            status,
            answer,
            sources,
            DateTime.UtcNow
        );
    }

    // Logs the start of the reply (not all of it, to keep user content in logs short) and returns the 502 to throw
    private BadGatewayException BadFormat(Guid requestId, string reply)
    {
        logger.LogWarning("Request {RequestId}: could not read the model's reply. Start of reply: {ReplyStart}",
            requestId, reply.Length > 200 ? reply[..200] : reply);
        return new BadGatewayException("model_bad_format", "The model's answer could not be read.");
    }

    private static string? ReadTag(string text, string tag)
    {
        var match = Regex.Match(text, $"<{tag}>(.*?)</{tag}>", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
