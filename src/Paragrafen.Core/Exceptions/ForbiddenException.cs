namespace Paragrafen.Core.Exceptions;

public sealed class ForbiddenException(string action)
    : AppException("forbidden", "Forbidden", $"You are not allowed to {action}")
{ }
