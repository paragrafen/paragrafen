namespace Paragrafen.Core.Exceptions;

/// Conflicts are quite ambigious and difficult to generalize
/// Use this as for example:
/// ConflictException("email_already_registered", "That email address is already registered")
public sealed class ConflictException(string code, string message)
    : AppException(code, "Conflict", message)
{ }
