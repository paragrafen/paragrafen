namespace Paragrafen.Core.Abstractions;

public interface IAiModelService
{
    Task<string> CompleteAsync(string message, CancellationToken cancellationToken);
}
