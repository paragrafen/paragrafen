namespace Paragrafen.Core.Contracts.Results;

public record ResultsResponse(
    DateTime GeneratedAt,
    CallStats Total,
    List<DayStats> Days,
    List<RecentCall> RecentCalls
);

public record DayStats(DateOnly Date, CallStats Stats);

public record CallStats(
    int Calls,
    int Answered,
    int NotFound,
    int OutOfScope,
    int Errors,
    double ErrorRate,
    decimal CostNokTotal,
    decimal? CostNokMedian,
    decimal? CostNokP95,
    int? LatencyMsP50,
    int? LatencyMsP95,
    int? LlmLatencyMsP50,
    long InputTokens,
    long CachedTokens,
    long OutputTokens,
    long ReasoningTokens
);

public record RecentCall(
    Guid RequestId,
    DateTime CreatedAt,
    string Status,
    string? FailureReason,
    string? Model,
    int Attempts,
    int InputTokens,
    int OutputTokens,
    decimal CostNok,
    int LatencyMs
);
