using Paragrafen.Core.Models;

namespace Paragrafen.Core.Abstractions;

public interface IEmbeddingService
{
    Task<EmbeddingResult> EmbedAsync(string text, CancellationToken cancellationToken);
}
