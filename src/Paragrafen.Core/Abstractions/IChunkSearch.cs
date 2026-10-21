using Paragrafen.Core.Models;

namespace Paragrafen.Core.Abstractions;

public interface IChunkSearch
{
    Task<IReadOnlyList<ChunkMatch>> FindClosestAsync(
        ReadOnlyMemory<float> embedding, int limit, CancellationToken cancellationToken);
}
