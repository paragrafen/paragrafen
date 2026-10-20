namespace Paragrafen.Core.Abstractions;

public interface ICurrentUser
{
    Guid UserId { get; }
    bool IsInRole(string role);

    string UserName { get; }
}
