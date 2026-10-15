using Microsoft.AspNetCore.Diagnostics;
using Paragrafen.Core.Contracts;
using Paragrafen.Core.Exceptions;

namespace Paragrafen.Api.ExceptionHandlers;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        (int statusCode, string code, string message) = exception switch
        {
            // Can add more custom exceptions here. pls sort them by status code
            BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                "invalid_request",
                "The request could not be read. Check the query string and the JSON body."
            ),
            BadRequestException ex => (StatusCodes.Status400BadRequest, "invalid_request", ex.Message),
            UnauthorizedException ex => (StatusCodes.Status401Unauthorized, "unauthorized", ex.Message),
            ForbiddenException ex => (StatusCodes.Status403Forbidden, ex.Code, ex.Message),
            NotFoundException ex => (StatusCodes.Status404NotFound, ex.Code, ex.Message),
            ConflictException ex => (StatusCodes.Status409Conflict, ex.Code, ex.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "An unexpected error occurred."
            ),
        };

        var requestId = httpContext.GetRequestId();

        if (statusCode == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception for {Endpoint}, request {RequestId}",
                httpContext.GetEndpoint()?.DisplayName, requestId);
        else
            _logger.LogWarning("Request {RequestId} failed with {StatusCode} {Code}: {Message}",
                requestId, statusCode, (exception as AppException)?.Code ?? code, exception.Message);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new ErrorResponse(requestId, new ErrorDetail(code, message)),
            cancellationToken);

        return true;
    }
}
