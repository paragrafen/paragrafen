using Paragrafen.Core.Contracts;

namespace Paragrafen.Core.Models;

public sealed class CallRecord(Guid requestId, Guid conversationId, DateTime createdAt)
{
    public Guid RequestId { get; } = requestId;
    public Guid ConversationId { get; } = conversationId;
    public DateTime CreatedAt { get; } = createdAt;
    public string Status { get; private set; } = "error";
    public string? FailureReason { get; private set; }
    public int Attempts { get; private set; }
    public string? Model { get; private set; }
    public int InputTokens { get; private set; }
    public int CachedTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public int ReasoningTokens { get; private set; }
    public decimal CostNok { get; private set; }
    public int LatencyMs { get; private set; }
    public int LlmLatencyMs { get; private set; }

    public void AddModelCall(ModelResult? result, TimeSpan elapsed, ModelPrice price)
    {
        Attempts++;
        LlmLatencyMs += (int)elapsed.TotalMilliseconds;
        if (result is null)
            return;

        Model = result.Model;
        InputTokens += result.InputTokens;
        CachedTokens += result.CachedTokens;
        OutputTokens += result.OutputTokens;
        ReasoningTokens += result.ReasoningTokens;
        CostNok += price.Cost(result);
    }

    public void Complete(AnswerStatus status) => Status = status switch
    {
        AnswerStatus.Answered => "answered",
        AnswerStatus.NotFound => "not_found",
        AnswerStatus.OutOfScope => "out_of_scope",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public void Fail(string reason)
    {
        Status = "error";
        FailureReason = reason;
    }

    public void Finish(TimeSpan elapsed) => LatencyMs = (int)elapsed.TotalMilliseconds;
}
