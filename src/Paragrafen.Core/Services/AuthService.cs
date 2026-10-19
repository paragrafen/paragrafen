
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Contracts.Authentication;
using Paragrafen.Core.Exceptions;

namespace Paragrafen.Core.Services;

public class AuthService(
    IIdentityService identityService,
    ITokenService tokenService,
    ILogger<AuthService> logger
    ) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var exists = await identityService.FindByEmailAsync(request.Email, cancellationToken);
        if (exists is not null)
        {
            logger.LogWarning("User with email '{Email}' already exists.", request.Email);
            throw new ConflictException(
                "email_already_registered",
                "The email address is already registered.");
        }

        var result = await identityService.CreateUserAsync(request, cancellationToken);
        if (!result.Success)
        {
            logger.LogWarning("Registration failed: {Codes}", string.Join(",", result.Errors.Select(e => e.Code)));
            if (result.Errors.Any(e => e.Code == "DuplicateUserName"))
                throw new ConflictException(
                    "username_taken",
                    "The username is already registered.");

            throw new ValidationException(result.Errors.Select(e =>
                new ValidationFailure(PropertyFor(e.Code), e.Description)));
        }

        var token = tokenService.CreateAccessToken(result.User!);

        return new AuthResponse(token.Value, token.ExpiresAt, result.User!);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await identityService.FindByEmailAsync(request.Email, cancellationToken);
        if (user is null || !await identityService.CheckPasswordAsync(user.Id, request.Password, cancellationToken))
        {
            logger.LogWarning("Login failed.");
            throw new UnauthorizedException("invalid_credentials", "Invalid email or password.");
        }

        var token = tokenService.CreateAccessToken(user);
        return new AuthResponse(token.Value, token.ExpiresAt, user);
    }

    // Private helper function
    private static string PropertyFor(string code) =>
        code switch
        {
            _ when code.Contains("Email", StringComparison.Ordinal) => nameof(RegisterRequest.Email),
            _ when code.Contains("UserName", StringComparison.Ordinal) => nameof(RegisterRequest.UserName),
            _ when code.Contains("Password", StringComparison.Ordinal) => nameof(RegisterRequest.Password),
            _ => string.Empty,
        };
}
