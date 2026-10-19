namespace Paragrafen.Core.Exceptions;

public sealed class BadGatewayException(string code, string message, Exception? innerException = null)
    : AppException(code, "Bad Gateway", message, innerException)
{ }
