using FluentValidation;
using PulsePilot.Domain.Feedback;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.Application.Feedback;

public sealed class UpdateFeedbackCommandValidator : AbstractValidator<UpdateFeedbackCommand>
{
    public UpdateFeedbackCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Title)
            .MaximumLength(FeedbackEntity.MaxTitleLength);
        RuleFor(command => command.Content)
            .NotEmpty()
            .MaximumLength(FeedbackEntity.MaxContentLength);
        RuleFor(command => command.Source)
            .Must(source => source is FeedbackSource.Manual or FeedbackSource.Api)
            .WithMessage("Source must be Manual or Api.");
        RuleFor(command => command.CustomerName)
            .MaximumLength(FeedbackEntity.MaxCustomerNameLength);
        RuleFor(command => command.CustomerEmail)
            .MaximumLength(FeedbackEntity.MaxCustomerEmailLength)
            .EmailAddress()
            .When(command => !string.IsNullOrWhiteSpace(command.CustomerEmail));
    }
}
