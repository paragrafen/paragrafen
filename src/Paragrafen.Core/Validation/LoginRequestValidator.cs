using FluentValidation;
using Paragrafen.Core.Contracts.Authentication;

namespace Paragrafen.Core.Validation;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(loginRequest => loginRequest.Email)
            .NotEmpty()
            .WithMessage("Email is required");

        RuleFor(loginRequest => loginRequest.Password)
            .NotEmpty()
            .WithMessage("Password is required");
    }
}
