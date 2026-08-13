using FluentValidation;
using PulsePilot.Domain.CustomerResponses;

namespace PulsePilot.Application.AI;

public sealed class CustomerResponseDraftResultValidator
    : AbstractValidator<CustomerResponseDraftResult>
{
    public CustomerResponseDraftResultValidator()
    {
        RuleFor(result => result.Content)
            .NotEmpty()
            .MaximumLength(CustomerResponseDraft.MaxContentLength)
            .Must(content => CountWords(content) <= CustomerResponseDraft.MaxWordCount)
            .WithMessage(
                $"Content cannot exceed {CustomerResponseDraft.MaxWordCount} words.");
    }

    private static int CountWords(string content)
    {
        return string.IsNullOrWhiteSpace(content)
            ? 0
            : content.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
