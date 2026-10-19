using Paragrafen.Core.Contracts;
using Paragrafen.Core.Models;
using Paragrafen.Data.Calls;

namespace Paragrafen.IntegrationTests;

// Guards the contract between CallRecord, CallLog and the calls table, so a migration or a code change that breaks logging fails here.
[Collection(PostgresCollection.Name)]
public class CallLogTests(PostgresFixture db)
{
    private static readonly ModelPrice Price = new(10m, 2.5m, 40m);
    private static readonly ModelResult Usage = new("svar", "gpt-6-luna-2026-09-22", 1000, 400, 500, 300);
    private static readonly DateTime CreatedAt = new(2026, 10, 19, 9, 0, 0, 123, DateTimeKind.Utc);

    [Fact]
    public async Task Answered_call_is_stored_with_every_field()
    {
        var record = new CallRecord(Guid.CreateVersion7(), Guid.CreateVersion7(), CreatedAt);
        record.AddModelCall(Usage, TimeSpan.FromMilliseconds(300), Price);
        record.Complete(AnswerStatus.Answered);
        record.Finish(TimeSpan.FromMilliseconds(1200));

        await new CallLog(db.DataSource).WriteAsync(record, CancellationToken.None);

        var row = await db.ReadCallAsync(record.RequestId);
        Assert.Equal(CreatedAt, ((DateTime)row["created_at"]).ToUniversalTime());
        Assert.Equal(record.ConversationId, row["conversation_id"]);
        Assert.Equal("answered", row["status"]);
        Assert.Equal(DBNull.Value, row["failure_reason"]);
        Assert.Equal((short)1, row["attempts"]);
        Assert.Equal("gpt-6-luna-2026-09-22", row["model"]);
        Assert.Equal(1000, row["input_tokens"]);
        Assert.Equal(400, row["cached_tokens"]);
        Assert.Equal(500, row["output_tokens"]);
        Assert.Equal(300, row["reasoning_tokens"]);
        Assert.Equal(0.027m, row["cost_nok"]);
        Assert.Equal(1200, row["latency_ms"]);
        Assert.Equal(300, row["llm_latency_ms"]);
    }

    [Fact]
    public async Task Call_that_never_reached_the_model_is_stored_with_nulls()
    {
        var record = new CallRecord(Guid.CreateVersion7(), Guid.CreateVersion7(), CreatedAt);
        record.Fail("empty_question");
        record.Finish(TimeSpan.FromMilliseconds(2));

        await new CallLog(db.DataSource).WriteAsync(record, CancellationToken.None);

        var row = await db.ReadCallAsync(record.RequestId);
        Assert.Equal("error", row["status"]);
        Assert.Equal("empty_question", row["failure_reason"]);
        Assert.Equal((short)0, row["attempts"]);
        Assert.Equal(DBNull.Value, row["model"]);
        Assert.Equal(0, row["input_tokens"]);
        Assert.Equal(0m, row["cost_nok"]);
    }

    public static TheoryData<AnswerStatus> AnswerStatuses => new(Enum.GetValues<AnswerStatus>());

    [Theory]
    [MemberData(nameof(AnswerStatuses))]
    public async Task Every_answer_status_is_allowed_by_the_table(AnswerStatus status)
    {
        var record = new CallRecord(Guid.CreateVersion7(), Guid.CreateVersion7(), CreatedAt);
        record.AddModelCall(Usage, TimeSpan.FromMilliseconds(300), Price);
        record.Complete(status);
        record.Finish(TimeSpan.FromMilliseconds(400));

        await new CallLog(db.DataSource).WriteAsync(record, CancellationToken.None);

        Assert.Equal(record.Status, (await db.ReadCallAsync(record.RequestId))["status"]);
    }
}
