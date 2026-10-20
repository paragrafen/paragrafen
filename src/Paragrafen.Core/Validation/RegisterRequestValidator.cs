using FluentValidation;
using Paragrafen.Core.Contracts.Authentication;

namespace Paragrafen.Core.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(registerRequest => registerRequest.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Invalid email address");

        RuleFor(registerRequest => registerRequest.UserName)
            .NotEmpty()
            .WithMessage("Username is required")
            .Length(3, 32)
            .WithMessage("Username must be between 3 and 32 characters")
            .Matches("^[a-zA-Z0-9_-]+$")
            .WithMessage("Username can only contain letters, numbers, underscores, and dashes.");

        RuleFor(registerRequest => registerRequest.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .Length(8, 32)
            .WithMessage("Password must be between 8 and 32 characters");
    }
}
