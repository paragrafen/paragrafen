using Paragrafen.Core.Models;

namespace Paragrafen.Core.Abstractions;

public interface ICallLog
{
    Task WriteAsync(CallRecord record, CancellationToken cancellationToken);
}
