using FluentValidation;
using Microsoft.Extensions.Options;
using PulsePilot.Application.Agents;

namespace PulsePilot.Application.Copilot;

public sealed class CopilotChatCommandValidator : AbstractValidator<CopilotChatCommand>
{
    public CopilotChatCommandValidator(IOptions<AgentOrchestrationOptions> options)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        var orchestrationOptions = options.Value;

        RuleFor(command => command.Message)
            .NotEmpty()
            .MaximumLength(orchestrationOptions.MaxUserMessageLength);
    }
}
