using Paragrafen.Core.Models;

namespace Paragrafen.Core.Abstractions;

public interface IAiModelService
{
    Task<ModelResult> CompleteAsync(string message, CancellationToken cancellationToken);
}
