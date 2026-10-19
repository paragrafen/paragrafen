using Npgsql;
using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Models;

namespace Paragrafen.Data.Calls;

public sealed class CallLog(NpgsqlDataSource dataSource) : ICallLog
{
    public async Task WriteAsync(CallRecord record, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO calls (
                request_id, created_at, conversation_id, status, failure_reason, attempts, model,
                input_tokens, cached_tokens, output_tokens, reasoning_tokens, cost_nok, latency_ms, llm_latency_ms)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)
            """);

        command.Parameters.Add(new NpgsqlParameter { Value = record.RequestId });
        command.Parameters.Add(new NpgsqlParameter { Value = record.CreatedAt });
        command.Parameters.Add(new NpgsqlParameter { Value = record.ConversationId });
        command.Parameters.Add(new NpgsqlParameter { Value = record.Status });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)record.FailureReason ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = (short)record.Attempts });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)record.Model ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = record.InputTokens });
        command.Parameters.Add(new NpgsqlParameter { Value = record.CachedTokens });
        command.Parameters.Add(new NpgsqlParameter { Value = record.OutputTokens });
        command.Parameters.Add(new NpgsqlParameter { Value = record.ReasoningTokens });
        command.Parameters.Add(new NpgsqlParameter { Value = record.CostNok });
        command.Parameters.Add(new NpgsqlParameter { Value = record.LatencyMs });
        command.Parameters.Add(new NpgsqlParameter { Value = record.LlmLatencyMs });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
