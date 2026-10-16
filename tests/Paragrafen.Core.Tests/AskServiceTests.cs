using Microsoft.Extensions.Logging.Abstractions;
using Paragrafen.Core.Contracts;
using Paragrafen.Core.Exceptions;
using Paragrafen.Core.Models;
using Paragrafen.Core.Services;

namespace Paragrafen.Core.Tests;

public class AskServiceTests
{
    private const string Question = "Hvor gammel må man være for å stemme?";
    private static readonly ModelPrice Price = new(10m, 2.5m, 40m);
    private static readonly ModelResult Usage = new("Du må være 18 år.", "gpt-6-luna-2026-08-01", 1000, 400, 500, 300);

    private static AskService CreateService(FakeAiModelService model, FakeCallLog? callLog = null) =>
        new(model, callLog ?? new FakeCallLog(), Price, NullLogger<AskService>.Instance);

    [Fact]
    public async Task Model_text_is_returned_as_the_answer()
    {
        var response = await CreateService(FakeAiModelService.Returning(Usage))
            .AskAsync(new AskRequest(Question), CancellationToken.None, Guid.CreateVersion7());

        Assert.Equal(AnswerStatus.Answered, response.Status);
        Assert.Equal("Du må være 18 år.", response.Answer);
    }

    [Fact]
    public async Task Empty_model_answer_is_a_bad_gateway()
    {
        var service = CreateService(FakeAiModelService.Returning(Usage with { Text = " " }));

        var ex = await Assert.ThrowsAsync<BadGatewayException>(() =>
            service.AskAsync(new AskRequest(Question), CancellationToken.None, Guid.CreateVersion7()));

        Assert.Equal("model_no_answer", ex.Code);
    }

    [Fact]
    public async Task Answered_question_writes_one_record_with_usage_and_cost()
    {
        var callLog = new FakeCallLog();
        var requestId = Guid.CreateVersion7();

        var response = await CreateService(FakeAiModelService.Returning(Usage), callLog)
            .AskAsync(new AskRequest(Question), CancellationToken.None, requestId);

        var record = Assert.Single(callLog.Written);
        Assert.Equal(requestId, record.RequestId);
        Assert.Equal(response.ConversationId, record.ConversationId);
        Assert.Equal(response.AskedAt, record.CreatedAt);
        Assert.Equal("answered", record.Status);
        Assert.Null(record.FailureReason);
        Assert.Equal(1, record.Attempts);
        Assert.Equal("gpt-6-luna-2026-08-01", record.Model);
        Assert.Equal(1000, record.InputTokens);
        Assert.Equal(400, record.CachedTokens);
        Assert.Equal(500, record.OutputTokens);
        Assert.Equal(300, record.ReasoningTokens);
        Assert.Equal(0.027m, record.CostNok);
        Assert.True(record.LatencyMs >= record.LlmLatencyMs);
    }

    [Fact]
    public async Task Given_conversation_id_is_recorded()
    {
        var callLog = new FakeCallLog();
        var conversationId = Guid.CreateVersion7();

        await CreateService(FakeAiModelService.Returning(Usage), callLog)
            .AskAsync(new AskRequest(Question, conversationId), CancellationToken.None, Guid.CreateVersion7());

        Assert.Equal(conversationId, Assert.Single(callLog.Written).ConversationId);
    }

    [Fact]
    public async Task Invalid_question_is_recorded_without_a_model_call()
    {
        var callLog = new FakeCallLog();

        await Assert.ThrowsAsync<BadRequestException>(() => CreateService(FakeAiModelService.Returning(Usage), callLog)
            .AskAsync(new AskRequest("   "), CancellationToken.None, Guid.CreateVersion7()));

        var record = Assert.Single(callLog.Written);
        Assert.Equal("error", record.Status);
        Assert.Equal("empty_question", record.FailureReason);
        Assert.Equal(0, record.Attempts);
        Assert.Null(record.Model);
        Assert.Equal(0m, record.CostNok);
    }

    [Fact]
    public async Task Failed_model_call_counts_as_an_attempt_without_tokens()
    {
        var callLog = new FakeCallLog();
        var model = FakeAiModelService.Throwing(new ServiceUnavailableException("model_busy", "busy"));

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => CreateService(model, callLog)
            .AskAsync(new AskRequest(Question), CancellationToken.None, Guid.CreateVersion7()));

        var record = Assert.Single(callLog.Written);
        Assert.Equal("error", record.Status);
        Assert.Equal("model_busy", record.FailureReason);
        Assert.Equal(1, record.Attempts);
        Assert.Equal(0, record.InputTokens);
        Assert.Equal(0m, record.CostNok);
    }

    [Fact]
    public async Task Empty_model_answer_still_records_the_tokens_we_paid_for()
    {
        var callLog = new FakeCallLog();
        var model = FakeAiModelService.Returning(Usage with { Text = "" });

        await Assert.ThrowsAsync<BadGatewayException>(() => CreateService(model, callLog)
            .AskAsync(new AskRequest(Question), CancellationToken.None, Guid.CreateVersion7()));

        var record = Assert.Single(callLog.Written);
        Assert.Equal("model_no_answer", record.FailureReason);
        Assert.Equal(1000, record.InputTokens);
        Assert.Equal(0.027m, record.CostNok);
    }

    [Fact]
    public async Task Unexpected_exception_is_recorded_as_internal_error()
    {
        var callLog = new FakeCallLog();
        var model = FakeAiModelService.Throwing(new InvalidOperationException("bug"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(model, callLog)
            .AskAsync(new AskRequest(Question), CancellationToken.None, Guid.CreateVersion7()));

        Assert.Equal("internal_error", Assert.Single(callLog.Written).FailureReason);
    }

    [Fact]
    public async Task Cancelled_request_is_still_recorded_without_the_request_token()
    {
        var callLog = new FakeCallLog();
        var model = new FakeAiModelService(ct =>
        {
            ct.ThrowIfCancellationRequested();
            return Usage;
        });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService(model, callLog)
            .AskAsync(new AskRequest(Question), cts.Token, Guid.CreateVersion7()));

        Assert.Equal("cancelled", Assert.Single(callLog.Written).FailureReason);
        Assert.False(callLog.LastToken.IsCancellationRequested);
        Assert.NotEqual(cts.Token, callLog.LastToken);
    }

    [Fact]
    public async Task Failing_call_log_does_not_fail_the_request()
    {
        var callLog = new FakeCallLog { ThrowOnWrite = true };

        var response = await CreateService(FakeAiModelService.Returning(Usage), callLog)
            .AskAsync(new AskRequest(Question), CancellationToken.None, Guid.CreateVersion7());

        Assert.Equal(AnswerStatus.Answered, response.Status);
        Assert.Equal("Du må være 18 år.", response.Answer);
    }
}
