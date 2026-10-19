namespace Paragrafen.Core.Contracts;

public record ErrorResponse(
    Guid RequestId,
    ErrorDetail Error
);

public record ErrorDetail(
    string Code,
    string Message
);
