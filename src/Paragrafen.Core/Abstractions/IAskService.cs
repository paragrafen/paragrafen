using Paragrafen.Core.Contracts;

namespace Paragrafen.Core.Abstractions;

public interface IAskService
{
    Task<AskResponse> AskAsync(AskRequest request, CancellationToken cancellationToken, Guid requestId);
}
