using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
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

    private readonly FakeTimeProvider _time = new();
    private readonly FakeLogger<AskService> _logger = new();

    private AskService CreateService(FakeAiModelService model, FakeCallLog? callLog = null) =>
        new(model, callLog ?? new FakeCallLog(), Price, _time, _logger);

    private FakeAiModelService TakingTime(TimeSpan duration, Func<ModelResult> complete) =>
        new(_ =>
        {
            _time.Advance(duration);
            return complete();
        });

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
    }

    [Fact]
    public async Task Record_uses_the_time_the_request_started()
    {
        var callLog = new FakeCallLog();
        var started = _time.GetUtcNow().UtcDateTime;

        await CreateService(TakingTime(TimeSpan.FromSeconds(2), () => Usage), callLog)
            .AskAsync(new AskRequest(Question), CancellationToken.None, Guid.CreateVersion7());

        Assert.Equal(started, Assert.Single(callLog.Written).CreatedAt);
    }

    [Fact]
    public async Task Latency_covers_the_whole_request_and_the_model_call_separately()
    {
        var callLog = new FakeCallLog();

        await CreateService(TakingTime(TimeSpan.FromMilliseconds(300), () => Usage), callLog)
            .AskAsync(new AskRequest(Question), CancellationToken.None, Guid.CreateVersion7());

        var record = Assert.Single(callLog.Written);
        Assert.Equal(300, record.LlmLatencyMs);
        Assert.Equal(300, record.LatencyMs);
    }

    [Fact]
    public async Task Failed_model_call_still_counts_its_latency()
    {
        var callLog = new FakeCallLog();
        var model = TakingTime(TimeSpan.FromMilliseconds(200), () => throw new BadGatewayException("model_unavailable", "down"));

        await Assert.ThrowsAsync<BadGatewayException>(() => CreateService(model, callLog)
            .AskAsync(new AskRequest(Question), CancellationToken.None, Guid.CreateVersion7()));

        var record = Assert.Single(callLog.Written);
        Assert.Equal("model_unavailable", record.FailureReason);
        Assert.Equal(1, record.Attempts);
        Assert.Equal(200, record.LlmLatencyMs);
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
    public async Task Too_long_question_is_recorded_without_a_model_call()
    {
        var callLog = new FakeCallLog();

        await Assert.ThrowsAsync<BadRequestException>(() => CreateService(FakeAiModelService.Returning(Usage), callLog)
            .AskAsync(new AskRequest(new string('a', 501)), CancellationToken.None, Guid.CreateVersion7()));

        var record = Assert.Single(callLog.Written);
        Assert.Equal("question_too_long", record.FailureReason);
        Assert.Equal(0, record.Attempts);
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

    [Fact]
    public async Task Failing_call_log_is_logged_as_an_error_with_the_request_id()
    {
        var requestId = Guid.CreateVersion7();

        await CreateService(FakeAiModelService.Returning(Usage), new FakeCallLog { ThrowOnWrite = true })
            .AskAsync(new AskRequest(Question), CancellationToken.None, requestId);

        var log = _logger.LatestRecord;
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.Contains(requestId.ToString(), log.Message);
        Assert.IsType<InvalidOperationException>(log.Exception);
    }

    [Fact]
    public async Task Hanging_call_log_is_cut_off_after_five_seconds()
    {
        var callLog = new FakeCallLog { Hang = true };

        var ask = CreateService(FakeAiModelService.Returning(Usage), callLog)
            .AskAsync(new AskRequest(Question), CancellationToken.None, Guid.CreateVersion7());
        await callLog.WriteStarted.Task;

        _time.Advance(TimeSpan.FromMilliseconds(4999));
        Assert.False(callLog.WriteCancelled);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(callLog.WriteCancelled);
        var response = await ask;

        Assert.Equal(AnswerStatus.Answered, response.Status);
        Assert.Equal(LogLevel.Error, _logger.LatestRecord.Level);
    }
}
