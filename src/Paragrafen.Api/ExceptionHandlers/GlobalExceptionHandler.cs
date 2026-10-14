using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Paragrafen.Core.Exceptions;
using System.ClientModel;
using Azure.Identity;

namespace Paragrafen.Api.ExceptionHandlers;



public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        (int statusCode, string title, string detail) = exception switch
        {
            // Can add more custom exceptions here. pls sort them by status code
            BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                "Invalid request",
                "The request could not be read. Check the query string and the JSON body."
            ),
            BadRequestException ex => (
                StatusCodes.Status400BadRequest,
                ex.Title,
                ex.Message
                ),

            UnauthorizedException ex => (StatusCodes.Status401Unauthorized, ex.Title, ex.Message),
            ForbiddenException ex => (StatusCodes.Status403Forbidden, ex.Title, ex.Message),
            NotFoundException ex => (StatusCodes.Status404NotFound, ex.Title, ex.Message),
            ConflictException ex => (StatusCodes.Status409Conflict, ex.Title, ex.Message),
            BadGatewayException ex => (StatusCodes.Status502BadGateway, ex.Title, ex.Message),
            ServiceUnavailableException ex => (StatusCodes.Status503ServiceUnavailable, ex.Title, ex.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                "An Unexpected error occured."
            ),
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception for {Endpoint}", httpContext.GetEndpoint()?.DisplayName);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
        };

        if (exception is AppException appException)
            problemDetails.Extensions["code"] = appException.Code;
        httpContext.Response.StatusCode = statusCode;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }
}
