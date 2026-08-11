using FluentValidation;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;

namespace PulsePilot.Application.Authentication;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public const int MinimumPasswordLength = 12;
    public const int MaximumPasswordLength = 128;

    public RegisterCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(User.MaxEmailLength)
            .EmailAddress();
        RuleFor(command => command.DisplayName)
            .NotEmpty()
            .MaximumLength(User.MaxDisplayNameLength);
        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(MinimumPasswordLength)
            .MaximumLength(MaximumPasswordLength);
        RuleFor(command => command.WorkspaceName)
            .NotEmpty()
            .MaximumLength(Workspace.MaxNameLength);
    }
}
