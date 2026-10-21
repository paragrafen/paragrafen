namespace Paragrafen.Core.Models;

public sealed record EmbeddingResult(
    ReadOnlyMemory<float> Vector,
    string Model,
    int InputTokens
);
