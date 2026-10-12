namespace Paragrafen.Core.Contracts;

public record Source(
    string ChunkId,
    string law,
    string Description,
    string Section,
    Uri Url
    );
