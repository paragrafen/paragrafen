using Microsoft.AspNetCore.Http.HttpResults;
using Paragrafen.Api.Filters;
using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Contracts;
using Paragrafen.Core.Contracts.Authentication;

namespace Paragrafen.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("").WithTags("Authentication").AllowAnonymous();

        group.MapPost("/register", Register)
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        group.MapPost("/login", Login)
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<Created<AuthResponse>> Register(
        RegisterRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var response = await authService.RegisterAsync(request, cancellationToken);
        return TypedResults.Created((string?)null, response);
    }

    private static async Task<Ok<AuthResponse>> Login(
        LoginRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, cancellationToken);
        return TypedResults.Ok(response);
    }
}
