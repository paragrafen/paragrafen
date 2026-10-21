using Paragrafen.Core.Contracts.Results;

namespace Paragrafen.Core.Abstractions;

public interface IResultsReader
{
    Task<ResultsResponse> ReadAsync(CancellationToken cancellationToken);
}
