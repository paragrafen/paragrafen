using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Contracts;
using Paragrafen.Core.Exceptions;
using Paragrafen.Core.Prompts;
using Paragrafen.Core.Models;

namespace Paragrafen.Core.Services;

public class AskService(
    IAiModelService aiModelService,
    AskPrompt prompt,
    ICallLog callLog,
    ModelPrice modelPrice,
    TimeProvider timeProvider,
    ILogger<AskService> logger
) : IAskService
{
    private static readonly TimeSpan CallLogTimeout = TimeSpan.FromSeconds(5);

    public async Task<AskResponse> AskAsync(AskRequest request, CancellationToken cancellationToken, Guid requestId)
    {
        var started = timeProvider.GetTimestamp();
        var record = new CallRecord(requestId, request.ConversationId ?? Guid.CreateVersion7(), timeProvider.GetUtcNow().UtcDateTime);

        try
        {
            var response = await AnswerAsync(request, record, cancellationToken);
            record.Complete(response.Status);
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            record.Fail("cancelled");
            throw;
        }
        catch (Exception ex)
        {
            record.Fail(ex is AppException appException ? appException.Code : "internal_error");
            throw;
        }
        finally
        {
            record.Finish(timeProvider.GetElapsedTime(started));
            await WriteCallLogAsync(record);
        }
    }

    private async Task<AskResponse> AnswerAsync(AskRequest request, CallRecord record, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            throw new BadRequestException("empty_question", "Question must not be empty.");
        if (request.Question.Length > 500)
            throw new BadRequestException("question_too_long", "Question must be at most 500 characters.");

        // note: db is currently empty, so no documents are retrieved
        var sources = new List<Source>();
        var documents = "";
        var (system, user) = prompt.Render(request.Question, documents);

        var result = await CompleteAsync(system, user, record, cancellationToken);

        if (string.IsNullOrWhiteSpace(result.Text))
            throw new BadGatewayException("model_no_answer", "The model did not return anything");

        var modelStatus = ReadTag(result.Text, "status")?.ToUpperInvariant();
        var status = modelStatus switch
        {
            "ANSWERED" => AnswerStatus.Answered,
            "PARTIAL" => AnswerStatus.Answered, // note: AnswerStatus has no Partial yet
            "NOT_FOUND" => AnswerStatus.NotFound,
            "OUT_OF_SCOPE" => AnswerStatus.OutOfScope,
            _ => throw BadFormat(record.RequestId, result.Text)
        };

        var answer = ReadTag(result.Text, "answer")
            ?? throw BadFormat(record.RequestId, result.Text);

        // if the model answers without citing sources
        if (status == AnswerStatus.Answered && sources.Count == 0)
        {
            status = AnswerStatus.NotFound;
            answer = NotFoundAnswer; // only a suggestion for what to send to frontend, can be changed
        }

        // <limitations> is optional; becomes the last paragraph of the answer when present
        var limitations = ReadTag(result.Text, "limitations");
        if (status == AnswerStatus.Answered && !string.IsNullOrWhiteSpace(limitations))
            answer = $"{answer}\n\n{limitations}";

        logger.LogInformation(
            "Request {RequestId}: prompt {PromptVersion}, model status {ModelStatus}, returned status {Status}",
            record.RequestId, prompt.Version, modelStatus, status);

        return new AskResponse(
            record.RequestId,
            record.ConversationId,
            status,
            answer,
            sources,
            record.CreatedAt
        );
    }

    // logs the start of the reply and returns the 502 to throw
    private BadGatewayException BadFormat(Guid requestId, string reply)
    {
        logger.LogWarning("Request {RequestId}: prompt {PromptVersion}, could not read the model's reply. Start of reply: {ReplyStart}",
            requestId, prompt.Version, reply.Length > 200 ? reply[..200] : reply);
        return new BadGatewayException("model_bad_format", "The model's answer could not be read.");
    }

    private async Task<ModelResult> CompleteAsync(string system, string user, CallRecord record, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        ModelResult? result = null;
        try
        {
            result = await aiModelService.CompleteAsync(system, user, cancellationToken);
            return result;
        }
        finally
        {
            record.AddModelCall(result, timeProvider.GetElapsedTime(started), modelPrice);
        }
    }

    private async Task WriteCallLogAsync(CallRecord record)
    {
        try
        {
            using var timeout = new CancellationTokenSource(CallLogTimeout, timeProvider);
            await callLog.WriteAsync(record, timeout.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not write call log for request {RequestId}", record.RequestId);
        }
    }

    private const string NotFoundAnswer = "Jeg fant ingen lovtekst som svarer på spørsmålet.";

    private static string? ReadTag(string text, string tag)
    {
        var match = Regex.Match(text, $"<{tag}>(.*?)</{tag}>", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
