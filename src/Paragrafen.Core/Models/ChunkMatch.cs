namespace Paragrafen.Core.Models;

public sealed record ChunkMatch(
    Guid ChunkId,
    string Text,
    string Law,
    string? SectionRef,
    string DokId,
    double Distance
);
