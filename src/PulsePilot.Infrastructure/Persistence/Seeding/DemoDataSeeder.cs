using System.Net.Mail;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Backlog;
using PulsePilot.Domain.CustomerResponses;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.Infrastructure.Persistence.Seeding;

internal sealed class DemoDataSeeder(
    AppDbContext dbContext,
    IPasswordHasher passwordHasher,
    IOptions<DemoSeedOptions> options) : IDemoDataSeeder
{
    private const int AnalyzedFeedbackTarget = 84;
    private const int ClusteredFeedbackTarget = 42;
    private const int FailedFeedbackTarget = 6;

    private static readonly FeedbackSource[] Sources =
    [
        FeedbackSource.Manual,
        FeedbackSource.Email,
        FeedbackSource.Support,
        FeedbackSource.Survey,
        FeedbackSource.Api,
        FeedbackSource.AppReview,
    ];

    private static readonly string[] CustomerNames =
    [
        "Avery Johnson",
        "Mina Patel",
        "Noah Williams",
        "Elif Demir",
        "Lucas Martin",
        "Sofia Rossi",
        "Liam Chen",
        "Amara Okafor",
        "Daniel Kim",
        "Leyla Kaya",
    ];

    private static readonly string[] ContextNotes =
    [
        "This started after the latest product update.",
        "The issue is reproducible on every attempt.",
        "Our team is blocked and needs a workaround.",
        "Several colleagues reported the same behavior.",
        "The previous version worked as expected.",
    ];

    private static readonly DemoFeedbackTemplate[] FeedbackTemplates =
    [
        new("Payments", "Card cannot be added", "I cannot add a new credit card at checkout."),
        new("Payments", "Subscription renewal failed", "Our subscription renewal keeps failing even though the card is valid."),
        new("Payments", "Duplicate invoice charge", "The latest invoice appears to have charged our company twice."),
        new("Payments", "Refund is still pending", "A confirmed refund has not appeared on the original payment method."),
        new("Authentication", "Login loop after sign-in", "Signing in redirects me back to the login screen instead of the dashboard."),
        new("Authentication", "Password reset link expired", "The password reset link is already expired when the email arrives."),
        new("Authentication", "Single sign-on stopped working", "Our SSO users receive an unauthorized message after authentication."),
        new("Authentication", "Two-factor code rejected", "Valid two-factor authentication codes are rejected repeatedly."),
        new("Dashboard", "Dashboard totals are stale", "The dashboard totals do not include feedback created today."),
        new("Dashboard", "Filters reset unexpectedly", "Selected dashboard filters disappear whenever I open an item."),
        new("Dashboard", "Chart labels overlap", "Chart labels overlap and make the weekly overview difficult to read."),
        new("Dashboard", "Critical issues widget is empty", "The critical issues widget is empty while matching feedback exists."),
        new("Mobile", "Mobile screen freezes", "The mobile application freezes when I open a feedback detail."),
        new("Mobile", "Push notification opens wrong item", "Tapping a push notification opens a different feedback record."),
        new("Mobile", "Cannot attach a screenshot", "Uploading a screenshot from the mobile application never completes."),
        new("Mobile", "Navigation covers action buttons", "The bottom navigation covers the approval buttons on smaller screens."),
        new("Reporting", "CSV export misses records", "The CSV export contains fewer feedback records than the report shows."),
        new("Reporting", "Scheduled report did not arrive", "The weekly scheduled report was not delivered to our team."),
        new("Reporting", "Date range uses wrong timezone", "Report date ranges shift by one day for our local timezone."),
        new("Reporting", "PDF report formatting is broken", "Long feedback titles are cut off in the generated PDF report."),
        new("Performance", "Feedback list loads slowly", "The feedback list takes more than ten seconds to appear."),
        new("Performance", "Search becomes unresponsive", "Searching a large workspace makes the page unresponsive."),
        new("Performance", "Dashboard refresh times out", "Refreshing dashboard metrics regularly ends with a timeout."),
        new("Performance", "Bulk import is too slow", "A small CSV import remains in progress for nearly an hour."),
        new("Feature Requests", "Add Slack notifications", "Please notify a Slack channel when a critical issue is detected."),
        new("Feature Requests", "Support custom categories", "We need workspace-specific categories for our product areas."),
        new("Feature Requests", "Allow saved dashboard views", "Let users save and share commonly used dashboard filters."),
        new("Feature Requests", "Add Jira issue creation", "We would like approved actions to create Jira issues automatically."),
    ];

    private static readonly DemoClusterDefinition[] ClusterDefinitions =
    [
        new("Checkout payment failures", "Payments:", FeedbackCategory.Bug,
            FeedbackComponent.Payments, 94m, FeedbackPriority.P1),
        new("SSO and login reliability", "Authentication:", FeedbackCategory.Bug,
            FeedbackComponent.Authentication, 88m, FeedbackPriority.P1),
        new("Dashboard data freshness", "Dashboard:", FeedbackCategory.Bug,
            FeedbackComponent.Dashboard, 76m, FeedbackPriority.P2),
        new("Mobile workflow stability", "Mobile:", FeedbackCategory.Bug,
            FeedbackComponent.Mobile, 71m, FeedbackPriority.P2),
        new("Report delivery and exports", "Reporting:", FeedbackCategory.Bug,
            FeedbackComponent.Reporting, 64m, FeedbackPriority.P2),
        new("Workspace performance degradation", "Performance:",
            FeedbackCategory.Complaint, FeedbackComponent.Performance, 58m,
            FeedbackPriority.P3),
        new("Requested workflow integrations", "Feature Requests:",
            FeedbackCategory.FeatureRequest, FeedbackComponent.General, 47m,
            FeedbackPriority.P3),
    ];

    public async Task<DemoSeedResult> SeedAsync(
        CancellationToken cancellationToken = default)
    {
        var seedOptions = options.Value;
        Validate(seedOptions);

        var now = DateTimeOffset.UtcNow;
        var identityCreatedAt = now.AddDays(-45);
        var normalizedEmail = seedOptions.Email.Trim().ToUpperInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (user is null)
        {
            user = User.Create(
                seedOptions.Email,
                seedOptions.DisplayName,
                passwordHasher.HashPassword(seedOptions.Password),
                identityCreatedAt);
            await dbContext.Users.AddAsync(user, cancellationToken);
        }
        else
        {
            SynchronizeUser(user, seedOptions, now);
        }

        var membership = await dbContext.WorkspaceMembers
            .Where(candidate => candidate.UserId == user.Id)
            .OrderBy(candidate => candidate.JoinedAt)
            .FirstOrDefaultAsync(cancellationToken);
        Workspace workspace;

        if (membership is null)
        {
            workspace = Workspace.Create(seedOptions.WorkspaceName, identityCreatedAt);
            membership = WorkspaceMember.Join(
                workspace.Id,
                user.Id,
                WorkspaceRole.Admin,
                identityCreatedAt);

            await dbContext.Workspaces.AddAsync(workspace, cancellationToken);
            await dbContext.WorkspaceMembers.AddAsync(membership, cancellationToken);
        }
        else
        {
            workspace = await dbContext.Workspaces.SingleAsync(
                candidate => candidate.Id == membership.WorkspaceId,
                cancellationToken);

            if (!string.Equals(
                    workspace.Name,
                    seedOptions.WorkspaceName,
                    StringComparison.Ordinal))
            {
                workspace.Rename(seedOptions.WorkspaceName, now);
            }

            if (membership.Role != WorkspaceRole.Admin)
            {
                membership.ChangeRole(WorkspaceRole.Admin);
            }
        }

        var existingFeedbackCount = await dbContext.Feedback.CountAsync(
            feedback => feedback.WorkspaceId == workspace.Id,
            cancellationToken);
        var addedFeedbackCount = Math.Max(
            0,
            seedOptions.FeedbackCount - existingFeedbackCount);

        if (addedFeedbackCount > 0)
        {
            var feedback = Enumerable
                .Range(existingFeedbackCount, addedFeedbackCount)
                .Select(index => CreateFeedback(
                    index,
                    seedOptions.FeedbackCount,
                    workspace.Id,
                    user.Id,
                    now));

            await dbContext.Feedback.AddRangeAsync(feedback, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await SeedProductStoryAsync(
            workspace.Id,
            user.Id,
            seedOptions.FeedbackCount,
            now,
            cancellationToken);

        return new DemoSeedResult(
            user.Id,
            workspace.Id,
            addedFeedbackCount,
            existingFeedbackCount + addedFeedbackCount);
    }

    private async Task SeedProductStoryAsync(
        Guid workspaceId,
        Guid userId,
        int feedbackCount,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var demoFeedback = (await dbContext.Feedback
                .Where(candidate => candidate.WorkspaceId == workspaceId
                    && candidate.CreatedByUserId == userId)
                .OrderBy(candidate => candidate.CreatedAt)
                .ThenBy(candidate => candidate.Id)
                .ToListAsync(cancellationToken))
            .Where(candidate => candidate.CustomerEmail?.StartsWith(
                "demo.customer",
                StringComparison.Ordinal) == true)
            .Take(feedbackCount)
            .ToList();

        if (demoFeedback.Count == 0)
        {
            return;
        }

        var analyzedFeedback = demoFeedback
            .TakeLast(Math.Min(AnalyzedFeedbackTarget, demoFeedback.Count))
            .ToList();
        var analyzedFeedbackIds = analyzedFeedback.Select(item => item.Id).ToHashSet();
        var existingAnalysisIds = await dbContext.FeedbackAnalyses
            .Where(candidate => candidate.WorkspaceId == workspaceId
                && analyzedFeedbackIds.Contains(candidate.FeedbackId))
            .Select(candidate => candidate.FeedbackId)
            .ToHashSetAsync(cancellationToken);

        foreach (var feedback in analyzedFeedback)
        {
            var profile = GetAnalysisProfile(feedback);
            var analyzedAt = feedback.CreatedAt.AddMinutes(12);

            if (!existingAnalysisIds.Contains(feedback.Id))
            {
                await dbContext.FeedbackAnalyses.AddAsync(
                    FeedbackAnalysis.Create(
                        workspaceId,
                        feedback.Id,
                        profile.Category,
                        profile.Component,
                        profile.Severity,
                        profile.Sentiment,
                        profile.Summary,
                        profile.SuggestedAction,
                        profile.Confidence,
                        analyzedAt),
                    cancellationToken);
            }

            CompleteFeedbackProcessing(feedback, analyzedAt);
        }

        foreach (var feedback in demoFeedback
            .Where(item => !analyzedFeedbackIds.Contains(item.Id))
            .Take(FailedFeedbackTarget))
        {
            FailFeedbackProcessing(feedback, feedback.CreatedAt.AddMinutes(8));
        }

        var existingClusters = await dbContext.FeedbackClusters
            .Where(candidate => candidate.WorkspaceId == workspaceId)
            .ToDictionaryAsync(candidate => candidate.Title, cancellationToken);
        var storyClusters = new Dictionary<string, FeedbackCluster>(
            StringComparer.Ordinal);

        foreach (var definition in ClusterDefinitions)
        {
            if (!existingClusters.TryGetValue(definition.Title, out var cluster))
            {
                cluster = FeedbackCluster.Create(
                    workspaceId,
                    definition.Title,
                    definition.Category,
                    definition.Component,
                    demoFeedback[0].CreatedAt);
                await dbContext.FeedbackClusters.AddAsync(cluster, cancellationToken);
            }

            if (cluster.PriorityScore != definition.Score
                || cluster.Priority != definition.Priority)
            {
                cluster.UpdatePriority(
                    definition.Score,
                    definition.Priority,
                    now.AddMinutes(-30));
            }

            storyClusters.Add(definition.Title, cluster);
        }

        foreach (var feedback in demoFeedback
            .TakeLast(Math.Min(ClusteredFeedbackTarget, demoFeedback.Count)))
        {
            var definition = GetClusterDefinition(feedback);

            if (definition is not null && !feedback.FeedbackClusterId.HasValue)
            {
                feedback.AssignToCluster(
                    storyClusters[definition.Title].Id,
                    feedback.CreatedAt.AddMinutes(15));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await SeedHumanReviewStoryAsync(
            workspaceId,
            userId,
            demoFeedback,
            storyClusters,
            now,
            cancellationToken);
    }

    private async Task SeedHumanReviewStoryAsync(
        Guid workspaceId,
        Guid userId,
        IReadOnlyList<FeedbackEntity> feedback,
        IReadOnlyDictionary<string, FeedbackCluster> clusters,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new DemoActionDefinition(
                "Checkout payment failures",
                PendingActionType.CreateEngineeringIssue,
                DemoActionState.Pending),
            new DemoActionDefinition(
                "SSO and login reliability",
                PendingActionType.CreateEngineeringIssue,
                DemoActionState.ExecutedBacklog),
            new DemoActionDefinition(
                "Dashboard data freshness",
                PendingActionType.CreateEngineeringIssue,
                DemoActionState.Pending),
            new DemoActionDefinition(
                "Report delivery and exports",
                PendingActionType.DraftCustomerResponse,
                DemoActionState.Rejected),
            new DemoActionDefinition(
                "Mobile workflow stability",
                PendingActionType.DraftCustomerResponse,
                DemoActionState.ExecutedDraft),
        };

        foreach (var definition in definitions)
        {
            var cluster = clusters[definition.ClusterTitle];
            var sourceFeedback = feedback.Last(candidate =>
                candidate.FeedbackClusterId == cluster.Id);
            var title = $"[{cluster.Priority}] {cluster.Title}";
            var action = await dbContext.PendingActions.SingleOrDefaultAsync(
                candidate => candidate.WorkspaceId == workspaceId
                    && candidate.Title == title
                    && candidate.ActionType == definition.ActionType,
                cancellationToken);

            if (action is null)
            {
                action = PendingAction.Create(
                    workspaceId,
                    sourceFeedback.Id,
                    cluster.Id,
                    definition.ActionType,
                    title,
                    CreateActionDescription(definition.ActionType, cluster),
                    CreateActionPayload(sourceFeedback, cluster),
                    now.AddDays(-2).AddMinutes((int)definition.State));

                if (definition.State == DemoActionState.Rejected)
                {
                    action.Reject(now.AddDays(-1));
                }
                else if (definition.State is DemoActionState.ExecutedBacklog
                    or DemoActionState.ExecutedDraft)
                {
                    action.Approve(now.AddDays(-1));
                    action.MarkExecuted(now.AddHours(-12));
                }

                await dbContext.PendingActions.AddAsync(action, cancellationToken);
            }

            if (definition.State == DemoActionState.ExecutedBacklog
                && !await dbContext.BacklogItems.AnyAsync(
                    candidate => candidate.WorkspaceId == workspaceId
                        && candidate.SourcePendingActionId == action.Id,
                    cancellationToken))
            {
                await dbContext.BacklogItems.AddAsync(
                    BacklogItem.Create(
                        workspaceId,
                        cluster.Id,
                        action.Id,
                        userId,
                        action.Title,
                        action.Description,
                        MapPriority(cluster.Priority),
                        action.ExecutedAt ?? now.AddHours(-12)),
                    cancellationToken);
            }

            if (definition.State == DemoActionState.ExecutedDraft
                && !await dbContext.CustomerResponseDrafts.AnyAsync(
                    candidate => candidate.WorkspaceId == workspaceId
                        && candidate.SourcePendingActionId == action.Id,
                    cancellationToken))
            {
                await dbContext.CustomerResponseDrafts.AddAsync(
                    CustomerResponseDraft.Create(
                        workspaceId,
                        sourceFeedback.Id,
                        cluster.Id,
                        action.Id,
                        userId,
                        "Thank you for reporting this. We have reproduced the mobile workflow issue and linked it to an active engineering investigation. The team is working on a fix, and we will share an update as soon as it is verified. We appreciate the detailed context you provided.",
                        action.ExecutedAt ?? now.AddHours(-12)),
                    cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void CompleteFeedbackProcessing(
        FeedbackEntity feedback,
        DateTimeOffset analyzedAt)
    {
        if (feedback.ProcessingStatus == ProcessingStatus.Completed)
        {
            return;
        }

        if (feedback.ProcessingStatus == ProcessingStatus.Failed)
        {
            feedback.RetryProcessing(analyzedAt.AddMinutes(-2));
        }

        if (feedback.ProcessingStatus == ProcessingStatus.Pending)
        {
            feedback.StartProcessing(analyzedAt.AddMinutes(-1));
        }

        if (feedback.ProcessingStatus == ProcessingStatus.Processing)
        {
            feedback.CompleteProcessing(analyzedAt);
        }
    }

    private static void FailFeedbackProcessing(
        FeedbackEntity feedback,
        DateTimeOffset failedAt)
    {
        if (feedback.ProcessingStatus != ProcessingStatus.Pending)
        {
            return;
        }

        feedback.StartProcessing(failedAt.AddMinutes(-1));
        feedback.FailProcessing(failedAt);
    }

    private static DemoAnalysisProfile GetAnalysisProfile(FeedbackEntity feedback)
    {
        var definition = GetClusterDefinition(feedback)
            ?? throw new InvalidOperationException(
                $"Demo feedback '{feedback.Title}' does not have a story profile.");

        return definition.Component switch
        {
            FeedbackComponent.Payments => new(
                definition.Category, definition.Component, 5,
                FeedbackSentiment.Negative,
                "Customers cannot reliably complete or reconcile payment operations.",
                "Prioritize payment-path diagnostics and add regression coverage.", 0.97m),
            FeedbackComponent.Authentication => new(
                definition.Category, definition.Component, 4,
                FeedbackSentiment.Negative,
                "Authentication flows are blocking workspace access for affected users.",
                "Inspect SSO callbacks, session creation, and reset-token delivery.", 0.95m),
            FeedbackComponent.Dashboard => new(
                definition.Category, definition.Component, 3,
                FeedbackSentiment.Negative,
                "Dashboard state and metrics are inconsistent with recent feedback.",
                "Validate cache invalidation and preserve dashboard filter state.", 0.93m),
            FeedbackComponent.Mobile => new(
                definition.Category, definition.Component, 4,
                FeedbackSentiment.Negative,
                "Mobile users encounter blocking navigation and stability problems.",
                "Reproduce on supported screen sizes and add mobile regression tests.", 0.94m),
            FeedbackComponent.Reporting => new(
                definition.Category, definition.Component, 3,
                FeedbackSentiment.Negative,
                "Generated reports are incomplete, late, or formatted incorrectly.",
                "Audit export queries, timezone boundaries, and delivery telemetry.", 0.92m),
            FeedbackComponent.Performance => new(
                definition.Category, definition.Component, 3,
                FeedbackSentiment.Negative,
                "Core workspace operations degrade as feedback volume increases.",
                "Profile slow queries and establish latency budgets for large workspaces.", 0.91m),
            _ => new(
                definition.Category, definition.Component, 2,
                FeedbackSentiment.Neutral,
                "Customers are requesting workflow integrations and customization.",
                "Validate demand and score the request against the product roadmap.", 0.89m),
        };
    }

    private static DemoClusterDefinition? GetClusterDefinition(FeedbackEntity feedback)
    {
        return ClusterDefinitions.FirstOrDefault(definition =>
            feedback.Title?.StartsWith(definition.TitlePrefix, StringComparison.Ordinal) == true);
    }

    private static string CreateActionDescription(
        PendingActionType actionType,
        FeedbackCluster cluster)
    {
        var verb = actionType == PendingActionType.CreateEngineeringIssue
            ? "Create an engineering issue"
            : "Draft a customer response";

        return $"{verb} for the {cluster.Priority} cluster '{cluster.Title}' after human review.";
    }

    private static string CreateActionPayload(
        FeedbackEntity feedback,
        FeedbackCluster cluster)
    {
        return JsonSerializer.Serialize(new
        {
            feedbackId = feedback.Id,
            feedbackClusterId = cluster.Id,
            priority = cluster.Priority.ToString(),
            priorityScore = cluster.PriorityScore,
            category = cluster.Category.ToString(),
            component = cluster.Component.ToString(),
            suggestedAction = "Review the evidence before executing this demo action.",
        });
    }

    private static BacklogItemPriority MapPriority(FeedbackPriority priority)
    {
        return priority switch
        {
            FeedbackPriority.P1 => BacklogItemPriority.P1,
            FeedbackPriority.P2 => BacklogItemPriority.P2,
            FeedbackPriority.P3 => BacklogItemPriority.P3,
            FeedbackPriority.P4 => BacklogItemPriority.P4,
            _ => throw new ArgumentOutOfRangeException(nameof(priority)),
        };
    }

    private void SynchronizeUser(
        User user,
        DemoSeedOptions seedOptions,
        DateTimeOffset now)
    {
        if (!string.Equals(
                user.DisplayName,
                seedOptions.DisplayName,
                StringComparison.Ordinal))
        {
            user.UpdateDisplayName(seedOptions.DisplayName, now);
        }

        if (!user.IsActive)
        {
            user.Activate(now);
        }

        if (passwordHasher.VerifyPassword(user.PasswordHash, seedOptions.Password)
            != PasswordVerificationStatus.Success)
        {
            user.ChangePasswordHash(passwordHasher.HashPassword(seedOptions.Password), now);
        }
    }

    private static FeedbackEntity CreateFeedback(
        int index,
        int feedbackCount,
        Guid workspaceId,
        Guid userId,
        DateTimeOffset now)
    {
        var template = FeedbackTemplates[index % FeedbackTemplates.Length];
        var customerNumber = index + 1;
        var createdAt = now.AddHours(-(feedbackCount - index) * 6L);

        return FeedbackEntity.Create(
            workspaceId,
            userId,
            $"{template.Category}: {template.Title}",
            $"{template.Content} {ContextNotes[index % ContextNotes.Length]}",
            Sources[index % Sources.Length],
            CustomerNames[index % CustomerNames.Length],
            $"demo.customer{customerNumber:D4}@example.com",
            createdAt);
    }

    private static void Validate(DemoSeedOptions seedOptions)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(seedOptions.Email)
            || seedOptions.Email.Length > User.MaxEmailLength
            || !MailAddress.TryCreate(seedOptions.Email.Trim(), out var parsedEmail)
            || !string.Equals(
                parsedEmail.Address,
                seedOptions.Email.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Seed email must be a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(seedOptions.Password)
            || seedOptions.Password.Length is < 12 or > 128)
        {
            failures.Add("Seed password must contain between 12 and 128 characters.");
        }

        if (string.IsNullOrWhiteSpace(seedOptions.DisplayName)
            || seedOptions.DisplayName.Trim().Length > User.MaxDisplayNameLength)
        {
            failures.Add(
                $"Seed display name is required and cannot exceed {User.MaxDisplayNameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(seedOptions.WorkspaceName)
            || seedOptions.WorkspaceName.Trim().Length > Workspace.MaxNameLength)
        {
            failures.Add(
                $"Seed workspace name is required and cannot exceed {Workspace.MaxNameLength} characters.");
        }

        if (seedOptions.FeedbackCount is < DemoSeedOptions.MinimumFeedbackCount
            or > DemoSeedOptions.MaximumFeedbackCount)
        {
            failures.Add(
                $"Seed feedback count must be between {DemoSeedOptions.MinimumFeedbackCount} and {DemoSeedOptions.MaximumFeedbackCount}.");
        }

        if (failures.Count > 0)
        {
            throw new OptionsValidationException(
                DemoSeedOptions.SectionName,
                typeof(DemoSeedOptions),
                failures);
        }
    }

    private sealed record DemoFeedbackTemplate(
        string Category,
        string Title,
        string Content);

    private sealed record DemoClusterDefinition(
        string Title,
        string TitlePrefix,
        FeedbackCategory Category,
        FeedbackComponent Component,
        decimal Score,
        FeedbackPriority Priority);

    private sealed record DemoAnalysisProfile(
        FeedbackCategory Category,
        FeedbackComponent Component,
        int Severity,
        FeedbackSentiment Sentiment,
        string Summary,
        string SuggestedAction,
        decimal Confidence);

    private sealed record DemoActionDefinition(
        string ClusterTitle,
        PendingActionType ActionType,
        DemoActionState State);

    private enum DemoActionState
    {
        Pending = 1,
        Rejected = 2,
        ExecutedBacklog = 3,
        ExecutedDraft = 4,
    }
}
