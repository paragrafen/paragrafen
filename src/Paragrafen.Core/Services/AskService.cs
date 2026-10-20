using Microsoft.Extensions.Logging;
using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Contracts;
using Paragrafen.Core.Exceptions;
using Paragrafen.Core.Models;

namespace Paragrafen.Core.Services;

public class AskService(
    IAiModelService aiModelService,
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

        var result = await CompleteAsync(request.Question, record, cancellationToken);

        if (string.IsNullOrWhiteSpace(result.Text))
            throw new BadGatewayException("model_no_answer", "The model did not return anything");

        return new AskResponse(
            record.RequestId,
            record.ConversationId,
            AnswerStatus.Answered,
            result.Text,
            new List<Source>(),
            record.CreatedAt
        );
    }

    private async Task<ModelResult> CompleteAsync(string message, CallRecord record, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        ModelResult? result = null;
        try
        {
            result = await aiModelService.CompleteAsync(message, cancellationToken);
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
}
