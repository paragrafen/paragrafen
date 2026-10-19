namespace Paragrafen.Core.Exceptions;

public sealed class ServiceUnavailableException(string code, string message, Exception? innerException = null)
    : AppException(code, "Service Unavailable", message, innerException)
{ }
