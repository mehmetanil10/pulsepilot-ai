using FluentValidation;
using PulsePilot.Domain.Users;

namespace PulsePilot.Application.Authentication;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(User.MaxEmailLength)
            .EmailAddress();
        RuleFor(command => command.Password)
            .NotEmpty()
            .MaximumLength(RegisterCommandValidator.MaximumPasswordLength);
    }
}
