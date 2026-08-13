using FluentValidation;
using Microsoft.Extensions.Options;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.AI;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.CustomerResponses;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Tools;

internal sealed class DraftCustomerResponseTool(
    ICustomerResponseDraftRepository customerResponseDraftRepository,
    IFeedbackRepository feedbackRepository,
    IFeedbackAnalysisRepository feedbackAnalysisRepository,
    ILLMClient llmClient,
    IValidator<CustomerResponseDraftResult> draftResultValidator,
    IOptions<CustomerResponseDraftingOptions> options) : IDraftCustomerResponseTool
{
    private readonly CustomerResponseDraftingOptions _options = options.Value;

    public async Task<CustomerResponseDraft> ExecuteAsync(
        PendingAction pendingAction,
        Guid createdByUserId,
        DateTimeOffset executedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pendingAction);

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Creator user id is required.", nameof(createdByUserId));
        }

        if (pendingAction.ActionType != PendingActionType.DraftCustomerResponse)
        {
            throw new ConflictException(
                "DraftCustomerResponseTool only accepts customer response actions.");
        }

        var existingDraft = await customerResponseDraftRepository
            .GetBySourcePendingActionIdAsync(
                pendingAction.WorkspaceId,
                pendingAction.Id,
                cancellationToken);

        if (existingDraft is not null)
        {
            if (pendingAction.Status == PendingActionStatus.Approved)
            {
                pendingAction.MarkExecuted(executedAt);
            }
            else if (pendingAction.Status != PendingActionStatus.Executed)
            {
                throw CreateApprovalRequiredConflict();
            }

            return existingDraft;
        }

        if (pendingAction.Status != PendingActionStatus.Approved)
        {
            throw CreateApprovalRequiredConflict();
        }

        var feedback = await feedbackRepository.GetByIdAsync(
            pendingAction.WorkspaceId,
            pendingAction.FeedbackId,
            cancellationToken)
            ?? throw new NotFoundException("Feedback", pendingAction.FeedbackId);
        var analysis = await feedbackAnalysisRepository.GetByFeedbackIdAsync(
            pendingAction.WorkspaceId,
            pendingAction.FeedbackId,
            cancellationToken);

        if (feedback.ProcessingStatus != ProcessingStatus.Completed || analysis is null)
        {
            throw new ConflictException(
                "Customer response drafting requires completed feedback analysis.");
        }

        var result = await GenerateWithRetryAsync(
            new CustomerResponseDraftRequest(
                feedback.Id,
                feedback.Title,
                feedback.Content,
                analysis.Category,
                analysis.Component,
                analysis.Severity,
                analysis.Sentiment,
                analysis.Summary),
            cancellationToken);
        var validationResult = await draftResultValidator.ValidateAsync(
            result,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new LlmProviderException(
                LlmProviderFailureKind.InvalidResponse,
                "The AI provider returned a customer response draft outside the accepted contract.");
        }

        var draft = CustomerResponseDraft.Create(
            pendingAction.WorkspaceId,
            feedback.Id,
            pendingAction.FeedbackClusterId,
            pendingAction.Id,
            createdByUserId,
            result.Content.Trim(),
            executedAt);

        await customerResponseDraftRepository.AddAsync(draft, cancellationToken);
        pendingAction.MarkExecuted(executedAt);

        return draft;
    }

    private async Task<CustomerResponseDraftResult> GenerateWithRetryAsync(
        CustomerResponseDraftRequest request,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            try
            {
                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
                timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

                return await llmClient.GenerateResponseDraftAsync(
                    request,
                    timeoutSource.Token);
            }
            catch (LlmProviderException exception)
                when (exception.IsTransient && attempt < _options.MaxAttempts)
            {
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == _options.MaxAttempts)
                {
                    throw new LlmProviderException(
                        LlmProviderFailureKind.ProviderUnavailable,
                        "Customer response drafting timed out.",
                        isTransient: true);
                }
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(_options.RetryDelayMilliseconds * attempt),
                cancellationToken);
        }

        throw new InvalidOperationException(
            "Customer response drafting retry loop completed unexpectedly.");
    }

    private static ConflictException CreateApprovalRequiredConflict()
    {
        return new ConflictException(
            "DraftCustomerResponseTool requires an approved pending action.");
    }
}
