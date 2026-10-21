using Paragrafen.Core.Models;

namespace Paragrafen.Core.Abstractions;

public interface IAiModelService
{
    Task<ModelResult> CompleteAsync(string prompt, string message, CancellationToken cancellationToken);
}
