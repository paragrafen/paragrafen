namespace Paragrafen.Core.Exceptions;

public class UnauthorizedException(string code, string message)
    : AppException(code, "Unauthorized", message)
{ }
