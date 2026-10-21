using Npgsql;
using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Contracts.Results;

namespace Paragrafen.Data.Calls;

public sealed class ResultsReader(NpgsqlDataSource dataSource, TimeProvider timeProvider) : IResultsReader
{
    private const int DayCount = 14;
    private const int RecentCallCount = 20;

    // Percentiles only count calls that reached the model, so validation failures (0 NOK, a few ms) don't pull them down.
    private const string StatsColumns = """
        count(*)::int,
        (count(*) FILTER (WHERE status = 'answered'))::int,
        (count(*) FILTER (WHERE status = 'not_found'))::int,
        (count(*) FILTER (WHERE status = 'out_of_scope'))::int,
        (count(*) FILTER (WHERE status = 'error'))::int,
        coalesce(sum(cost_nok), 0),
        round((percentile_cont(0.5) WITHIN GROUP (ORDER BY cost_nok) FILTER (WHERE attempts > 0))::numeric, 6),
        round((percentile_cont(0.95) WITHIN GROUP (ORDER BY cost_nok) FILTER (WHERE attempts > 0))::numeric, 6),
        round(percentile_cont(0.5) WITHIN GROUP (ORDER BY latency_ms) FILTER (WHERE attempts > 0))::int,
        round(percentile_cont(0.95) WITHIN GROUP (ORDER BY latency_ms) FILTER (WHERE attempts > 0))::int,
        round(percentile_cont(0.5) WITHIN GROUP (ORDER BY llm_latency_ms) FILTER (WHERE attempts > 0))::int,
        coalesce(sum(input_tokens), 0)::bigint,
        coalesce(sum(cached_tokens), 0)::bigint,
        coalesce(sum(output_tokens), 0)::bigint,
        coalesce(sum(reasoning_tokens), 0)::bigint
        """;

    public async Task<ResultsResponse> ReadAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var batch = dataSource.CreateBatch();
        batch.BatchCommands.Add(new NpgsqlBatchCommand($"SELECT {StatsColumns} FROM calls"));
        batch.BatchCommands.Add(new NpgsqlBatchCommand($"""
            SELECT (created_at AT TIME ZONE 'Europe/Oslo')::date AS day, {StatsColumns}
            FROM calls
            WHERE (created_at AT TIME ZONE 'Europe/Oslo')::date > ($1 AT TIME ZONE 'Europe/Oslo')::date - $2
            GROUP BY day
            ORDER BY day DESC
            """)
        {
            Parameters = { new NpgsqlParameter { Value = now }, new NpgsqlParameter { Value = DayCount } },
        });
        batch.BatchCommands.Add(new NpgsqlBatchCommand("""
            SELECT request_id, created_at, status, failure_reason, model, attempts,
                   input_tokens, output_tokens, cost_nok, latency_ms
            FROM calls
            ORDER BY created_at DESC
            LIMIT $1
            """)
        {
            Parameters = { new NpgsqlParameter { Value = RecentCallCount } },
        });

        await using var reader = await batch.ExecuteReaderAsync(cancellationToken);

        await reader.ReadAsync(cancellationToken);
        var total = ReadStats(reader, 0);

        await reader.NextResultAsync(cancellationToken);
        var days = new List<DayStats>();
        while (await reader.ReadAsync(cancellationToken))
            days.Add(new DayStats(reader.GetFieldValue<DateOnly>(0), ReadStats(reader, 1)));

        await reader.NextResultAsync(cancellationToken);
        var recentCalls = new List<RecentCall>();
        while (await reader.ReadAsync(cancellationToken))
        {
            recentCalls.Add(new RecentCall(
                RequestId: reader.GetGuid(0),
                CreatedAt: reader.GetDateTime(1),
                Status: reader.GetString(2),
                FailureReason: reader.IsDBNull(3) ? null : reader.GetString(3),
                Model: reader.IsDBNull(4) ? null : reader.GetString(4),
                Attempts: reader.GetInt16(5),
                InputTokens: reader.GetInt32(6),
                OutputTokens: reader.GetInt32(7),
                CostNok: reader.GetDecimal(8),
                LatencyMs: reader.GetInt32(9)));
        }

        return new ResultsResponse(now.UtcDateTime, total, days, recentCalls);
    }

    private static CallStats ReadStats(NpgsqlDataReader reader, int first)
    {
        var calls = reader.GetInt32(first);
        var errors = reader.GetInt32(first + 4);
        return new CallStats(
            Calls: calls,
            Answered: reader.GetInt32(first + 1),
            NotFound: reader.GetInt32(first + 2),
            OutOfScope: reader.GetInt32(first + 3),
            Errors: errors,
            ErrorRate: calls == 0 ? 0 : (double)errors / calls,
            CostNokTotal: reader.GetDecimal(first + 5),
            CostNokMedian: OrNull<decimal>(reader, first + 6),
            CostNokP95: OrNull<decimal>(reader, first + 7),
            LatencyMsP50: OrNull<int>(reader, first + 8),
            LatencyMsP95: OrNull<int>(reader, first + 9),
            LlmLatencyMsP50: OrNull<int>(reader, first + 10),
            InputTokens: reader.GetInt64(first + 11),
            CachedTokens: reader.GetInt64(first + 12),
            OutputTokens: reader.GetInt64(first + 13),
            ReasoningTokens: reader.GetInt64(first + 14));
    }

    private static T? OrNull<T>(NpgsqlDataReader reader, int ordinal) where T : struct =>
        reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<T>(ordinal);
}
