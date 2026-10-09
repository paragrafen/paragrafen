namespace Paragrafen.Core.Contracts;

public record Source(
    Guid ChunkId,
    string Description,
    string Title,
    string Section,
    Uri Url
    );
