using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Paragrafen.Data.Calls;

namespace Paragrafen.IntegrationTests;

// The collection runs its tests one at a time, so emptying calls here doesn't race other tests.
[Collection(PostgresCollection.Name)]
public class ResultsReaderTests(PostgresFixture db) : IAsyncLifetime
{
    // 14:00 in Oslo (CEST, UTC+2), so the per-day window is 2026-10-08 to 2026-10-21.
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await using var command = db.DataSource.CreateCommand("TRUNCATE calls");
        await command.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Empty_log_gives_zeros_and_no_percentiles()
    {
        var results = await Reader().ReadAsync(CancellationToken.None);

        Assert.Equal(Now.UtcDateTime, results.GeneratedAt);
        Assert.Equal(0, results.Total.Calls);
        Assert.Equal(0, results.Total.ErrorRate);
        Assert.Equal(0m, results.Total.CostNokTotal);
        Assert.Null(results.Total.CostNokMedian);
        Assert.Null(results.Total.CostNokP95);
        Assert.Null(results.Total.LatencyMsP50);
        Assert.Null(results.Total.LatencyMsP95);
        Assert.Null(results.Total.LlmLatencyMsP50);
        Assert.Equal(0, results.Total.InputTokens);
        Assert.Empty(results.Days);
        Assert.Empty(results.RecentCalls);
    }

    [Fact]
    public async Task Totals_count_every_call_and_percentiles_only_calls_that_reached_the_model()
    {
        await SeedAsync();

        var total = (await Reader().ReadAsync(CancellationToken.None)).Total;

        Assert.Equal(6, total.Calls);
        Assert.Equal(3, total.Answered);
        Assert.Equal(1, total.NotFound);
        Assert.Equal(0, total.OutOfScope);
        Assert.Equal(2, total.Errors);
        Assert.Equal(2 / 6.0, total.ErrorRate, 6);
        Assert.Equal(1.07m, total.CostNokTotal);
        Assert.Equal(0.02m, total.CostNokMedian);
        Assert.Equal(0.808m, total.CostNokP95);
        Assert.Equal(3000, total.LatencyMsP50);
        Assert.Equal(8800, total.LatencyMsP95);
        Assert.Equal(2900, total.LlmLatencyMsP50);
        Assert.Equal(6500, total.InputTokens);
        Assert.Equal(400, total.CachedTokens);
        Assert.Equal(1150, total.OutputTokens);
        Assert.Equal(100, total.ReasoningTokens);
    }

    [Fact]
    public async Task Days_are_oslo_dates_from_the_last_14_days_newest_first()
    {
        await SeedAsync();

        var days = (await Reader().ReadAsync(CancellationToken.None)).Days;

        Assert.Equal(new[] { new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 20) }, days.Select(d => d.Date));

        var today = days[0].Stats;
        Assert.Equal(4, today.Calls);
        Assert.Equal(2, today.Answered);
        Assert.Equal(1, today.NotFound);
        Assert.Equal(1, today.Errors);
        Assert.Equal(0.25, today.ErrorRate, 6);
        Assert.Equal(0.07m, today.CostNokTotal);
        Assert.Equal(0.02m, today.CostNokMedian);
        Assert.Equal(0.038m, today.CostNokP95);
        Assert.Equal(2000, today.LatencyMsP50);
        Assert.Equal(3800, today.LatencyMsP95);
        Assert.Equal(3500, today.InputTokens);

        var yesterday = days[1].Stats;
        Assert.Equal(1, yesterday.Calls);
        Assert.Equal(1, yesterday.Errors);
        Assert.Equal(1.0, yesterday.ErrorRate, 6);
        Assert.Equal(0m, yesterday.CostNokMedian);
        Assert.Equal(3000, yesterday.LatencyMsP50);
    }

    [Fact]
    public async Task Recent_calls_are_newest_first_with_their_failure_reason()
    {
        var ids = await SeedAsync();

        var recent = (await Reader().ReadAsync(CancellationToken.None)).RecentCalls;

        Assert.Equal(new[] { ids[2], ids[1], ids[0], ids[3], ids[4], ids[5] }, recent.Select(c => c.RequestId));
        var failed = recent[0];
        Assert.Equal("error", failed.Status);
        Assert.Equal("empty_question", failed.FailureReason);
        Assert.Null(failed.Model);
        Assert.Equal(0, failed.Attempts);
        Assert.Equal(0m, failed.CostNok);
        Assert.Equal(2, failed.LatencyMs);
        Assert.Equal(new DateTime(2026, 10, 21, 10, 0, 0, DateTimeKind.Utc), failed.CreatedAt.ToUniversalTime());
        Assert.Equal("gpt-6-luna-2026-09-22", recent[1].Model);
        Assert.Equal(2000, recent[1].InputTokens);
        Assert.Equal(400, recent[1].OutputTokens);
    }

    [Fact]
    public async Task Recent_calls_stop_at_20()
    {
        for (var minute = 0; minute < 21; minute++)
            await InsertAsync(At(10, 21, 8, minute), "answered", 1, 0.01m, 1000, 900);

        var recent = (await Reader().ReadAsync(CancellationToken.None)).RecentCalls;

        Assert.Equal(20, recent.Count);
    }

    private ResultsReader Reader() => new(db.DataSource, new FakeTimeProvider(Now));

    // 22:30 UTC on the 20th is 00:30 in Oslo on the 21st. The 1st is outside the 14-day window.
    private async Task<List<Guid>> SeedAsync() =>
    [
        await InsertAsync(At(10, 21, 8, 0), "answered", 1, 0.02m, 2000, 1800, input: 1000, cached: 400, output: 200, reasoning: 100),
        await InsertAsync(At(10, 21, 9, 0), "answered", 1, 0.04m, 4000, 3500, input: 2000, output: 400),
        await InsertAsync(At(10, 21, 10, 0), "error", 0, 0m, 2, 0, reason: "empty_question"),
        await InsertAsync(At(10, 20, 22, 30), "not_found", 1, 0.01m, 1000, 900, input: 500, output: 50),
        await InsertAsync(At(10, 20, 12, 0), "error", 1, 0m, 3000, 2900, reason: "model_unavailable"),
        await InsertAsync(At(10, 1, 12, 0), "answered", 1, 1.00m, 10000, 9000, input: 3000, output: 500),
    ];

    private async Task<Guid> InsertAsync(DateTimeOffset createdAt, string status, short attempts, decimal cost,
        int latency, int llmLatency, int input = 0, int cached = 0, int output = 0, int reasoning = 0, string? reason = null)
    {
        var id = Guid.CreateVersion7();
        var model = status == "error" ? null : "gpt-6-luna-2026-09-22";
        await using var command = db.DataSource.CreateCommand("""
            INSERT INTO calls (
                request_id, created_at, conversation_id, status, failure_reason, attempts, model,
                input_tokens, cached_tokens, output_tokens, reasoning_tokens, cost_nok, latency_ms, llm_latency_ms)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)
            """);
        object?[] values =
            [id, createdAt, Guid.CreateVersion7(), status, reason, attempts, model, input, cached, output, reasoning, cost, latency, llmLatency];
        foreach (var value in values)
            command.Parameters.Add(new NpgsqlParameter { Value = value ?? DBNull.Value });
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private static DateTimeOffset At(int month, int day, int hour, int minute) =>
        new(2026, month, day, hour, minute, 0, TimeSpan.Zero);
}
