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
    IEmbeddingService embeddingService,
    IChunkSearch chunkSearch,
    AskPrompt prompt,
    ICallLog callLog,
    ModelPrice modelPrice,
    TimeProvider timeProvider,
    ILogger<AskService> logger
) : IAskService
{
    private static readonly TimeSpan CallLogTimeout = TimeSpan.FromSeconds(5);
    private const int MaxChunks = 5;

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

        // find law test frist
        var questionEmbedding = await embeddingService.EmbedAsync(request.Question, cancellationToken);
        var chunks = await chunkSearch.FindClosestAsync(questionEmbedding.Vector, MaxChunks, cancellationToken);

        logger.LogInformation(
            "Embedded question with {Model} ({Tokens} tokens, {Dimensions} dimensions), found {Count} chunks",
            questionEmbedding.Model, questionEmbedding.InputTokens, questionEmbedding.Vector.Length, chunks.Count);
        foreach (var chunk in chunks)
            logger.LogInformation("{Distance:F3} {Law} {Section}", chunk.Distance, chunk.Law, chunk.SectionRef);

        var (system, user) = prompt.Render(request.Question, AskPrompt.FormatDocuments(chunks));

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

        // <limitations> is optional; becomes the last paragraph of the answer when present.
        // Appended before the renumbering, so citations in it are renumbered and linked like the rest of the answer.
        var limitations = ReadTag(result.Text, "limitations");
        if (status == AnswerStatus.Answered && !string.IsNullOrWhiteSpace(limitations))
            answer = $"{answer}\n\n{limitations}";

        (answer, var sources) = WithCitedSources(answer, chunks);

        // if the model answers without citing sources
        if (status == AnswerStatus.Answered && sources.Count == 0)
        {
            status = AnswerStatus.NotFound;
            answer = NotFoundAnswer; // only a suggestion for what to send to frontend, can be changed
        }

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

    private async Task<ModelResult> CompleteAsync(string system, string message, CallRecord record, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        ModelResult? result = null;
        try
        {
            result = await aiModelService.CompleteAsync(system, message, cancellationToken);
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

    // The model cites document ids as [3], [2] (also tolerates [3, 2]): 1-based positions in chunks, in the order
    // AskPrompt.FormatDocuments numbered them. The user only sees the cited sources, so the citations are renumbered
    // in order of first appearance to match them: [1] is always sources[0]. Ids the model made up are dropped from
    // the text, so an answer citing only those ends up with no sources and is treated as not found.
    private static (string Answer, List<Source> Sources) WithCitedSources(string answer, IReadOnlyList<ChunkMatch> chunks)
    {
        var newIds = new Dictionary<int, int>();
        var sources = new List<Source>();

        var renumbered = Regex.Replace(answer, @"\[(\d+(?:\s*,\s*\d+)*)\]", match =>
        {
            var ids = match.Groups[1].Value.Split(',')
                .Select(id => int.TryParse(id.Trim(), out var number) ? number : 0)
                .Where(id => id >= 1 && id <= chunks.Count)
                .Select(id =>
                {
                    if (!newIds.TryGetValue(id, out var newId))
                    {
                        newId = newIds[id] = sources.Count + 1;
                        sources.Add(ToSource(chunks[id - 1]));
                    }
                    return newId;
                })
                .Distinct()
                .ToList();

            // One number per bracket ([1][2]), since the frontend only links single-number citations.
            return string.Concat(ids.Select(id => $"[{id}]"));
        });

        return (renumbered, sources);
    }

    private static Source ToSource(ChunkMatch chunk) => new(
        chunk.ChunkId.ToString(),
        chunk.Law,
        "", // note: no provision heading in ChunkMatch yet; agree with the frontend what goes here
        chunk.SectionRef ?? "",
        LovdataUrl(chunk.DokId, chunk.SectionRef));

    private static Uri LovdataUrl(string dokId, string? sectionRef)
    {
        var url = $"https://lovdata.no/dokument/{dokId}";

        if (string.IsNullOrWhiteSpace(sectionRef))
            return new Uri(url);

        var anchor = Uri.EscapeDataString(
            Regex.Replace(sectionRef, @"\s+", ""));
        return new Uri($"{url}/{anchor}");
    }
}
