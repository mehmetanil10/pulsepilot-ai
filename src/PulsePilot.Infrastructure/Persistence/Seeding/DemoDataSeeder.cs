using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PulsePilot.Application.Abstractions.Authentication;
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

        return new DemoSeedResult(
            user.Id,
            workspace.Id,
            addedFeedbackCount,
            existingFeedbackCount + addedFeedbackCount);
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
}
