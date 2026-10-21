namespace Paragrafen.Core.Contracts.Results;

public record ResultsResponse(
    DateTime GeneratedAt,
    CallStats Total,           // every call ever logged
    List<DayStats> Days,       // last 14 days, newest first, days without calls left out
    List<RecentCall> RecentCalls // latest 20, newest first
);

public record DayStats(DateOnly Date, CallStats Stats); // Date is in Europe/Oslo

// Median and p95 only count calls that reached the model (attempts > 0); null when there are none.
// p50 = median: half the calls were at or below it. p95: 95% were at or below it, the worst 5% above.
public record CallStats(
    int Calls,
    int Answered,
    int NotFound,
    int OutOfScope,
    int Errors,
    double ErrorRate,          // Errors / Calls, 0 to 1, over every call
    decimal CostNokTotal,
    decimal? CostNokMedian,    // cost of a typical question
    decimal? CostNokP95,       // cost of the expensive ones
    int? LatencyMsP50,         // whole request, as the user waits for it
    int? LatencyMsP95,
    int? LlmLatencyMsP50,      // time spent waiting for the model only
    long InputTokens,
    long CachedTokens,         // part of InputTokens served from the prompt cache, billed cheaper
    long OutputTokens,
    long ReasoningTokens       // part of OutputTokens the model used for thinking
);

public record RecentCall(
    Guid RequestId,
    DateTime CreatedAt,
    string Status,             // answered, not_found, out_of_scope or error
    string? FailureReason,     // specific error code, e.g. empty_question or model_unavailable
    string? Model,             // null when the call never got an answer from the model
    int Attempts,              // model calls made, 0 if rejected before the model
    int InputTokens,
    int OutputTokens,
    decimal CostNok,
    int LatencyMs
);
