namespace Paragrafen.Core.Abstractions;

public interface IUserLookupService
{
    Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IEnumerable<Guid> userIds, CancellationToken cancellationToken);
}
