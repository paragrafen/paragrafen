namespace Paragrafen.Core.Models;

public sealed record ModelResult(
    string Text,
    string Model,
    int InputTokens,
    int CachedTokens,
    int OutputTokens,
    int ReasoningTokens
);
