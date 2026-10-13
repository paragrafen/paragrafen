namespace Paragrafen.Core.Exceptions;

public sealed class NotFoundException(string resource, object id)
    : AppException(
        $"{resource.ToLowerInvariant()}_not_found",
        $"{resource} not found",
        $"No {resource.ToLowerInvariant()} exists with id '{id}'."
    )
{ }
