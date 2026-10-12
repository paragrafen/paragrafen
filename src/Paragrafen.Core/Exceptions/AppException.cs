namespace Paragrafen.Core.Exceptions;

/// <summary>
/// Base class for errors the application raises intentionally.
/// </summary>
public abstract class AppException(string code, string title, string message) : Exception(message)
{
    public string Code { get; } = code;
    public string Title { get; } = title;
}

