namespace Paragrafen.Core.Abstractions;

public interface IAiModelService
{
    Task<string> CompleteAsync(string prompt, string message, CancellationToken cancellationToken);
}
