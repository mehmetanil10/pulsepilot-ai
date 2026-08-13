using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Authentication;
using PulsePilot.Application.Dashboard;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Feedback;
using PulsePilot.IntegrationTests.Persistence;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.IntegrationTests.Api;

public sealed class DashboardEndpointTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    [Fact]
    public async Task DashboardEndpoints_ReturnLiveWorkspaceScopedSummaryAndTrends()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        var owner = await CreateAuthenticatedClientAsync(factory, "dashboard-owner");
        var outsider = await CreateAuthenticatedClientAsync(factory, "dashboard-outsider");
        using var ownerClient = owner.Client;
        using var outsiderClient = outsider.Client;
        using var anonymousClient = factory.CreateClient();
        var seeded = await SeedDashboardAsync(factory, owner.Authentication);

        var summary = await ownerClient.GetFromJsonAsync<DashboardSummaryResponse>(
            "/api/dashboard/summary?periodDays=7&recentFeedbackLimit=2&pendingActionLimit=2",
            SerializerOptions);
        var trending = await ownerClient.GetFromJsonAsync<DashboardTrendingResponse>(
            "/api/dashboard/trending?periodDays=7&limit=3",
            SerializerOptions);
        var outsiderSummary = await outsiderClient.GetFromJsonAsync<DashboardSummaryResponse>(
            "/api/dashboard/summary",
            SerializerOptions);
        using var anonymousResponse = await anonymousClient.GetAsync("/api/dashboard/summary");
        using var invalidSummary = await ownerClient.GetAsync(
            "/api/dashboard/summary?periodDays=0&recentFeedbackLimit=11");
        using var invalidTrending = await ownerClient.GetAsync(
            "/api/dashboard/trending?limit=51");

        Assert.NotNull(summary);
        Assert.Equal(7, summary.PeriodDays);
        Assert.Equal(2, summary.Kpis.FeedbackToday);
        Assert.Equal(1, summary.Kpis.AiProcessed);
        Assert.Equal(1, summary.Kpis.CriticalIssues);
        Assert.Equal(1, summary.Kpis.PendingActions);
        Assert.Equal(1, summary.Kpis.ProcessingFailures);
        Assert.Equal(5m, summary.Kpis.AverageSeverity);
        Assert.Equal(2, summary.RecentFeedback.Count);
        Assert.DoesNotContain(
            summary.RecentFeedback,
            item => item.Id == seeded.PreviousFeedbackId);
        var pendingAction = Assert.Single(summary.PendingActions);
        Assert.Equal(seeded.PendingActionId, pendingAction.Id);
        Assert.Contains(
            summary.Categories,
            item => item.Category == FeedbackCategory.Bug && item.Count == 1);

        Assert.NotNull(trending);
        var issue = Assert.Single(trending.Items);
        Assert.Equal(seeded.ClusterId, issue.FeedbackClusterId);
        Assert.Equal(2, issue.CurrentPeriodCount);
        Assert.Equal(1, issue.PreviousPeriodCount);
        Assert.Equal(1, issue.DeltaCount);
        Assert.Equal(100m, issue.GrowthPercentage);
        Assert.False(issue.IsNew);

        Assert.NotNull(outsiderSummary);
        Assert.Equal(0, outsiderSummary.Kpis.FeedbackToday);
        Assert.Equal(0, outsiderSummary.Kpis.CriticalIssues);
        Assert.Empty(outsiderSummary.RecentFeedback);
        Assert.Empty(outsiderSummary.PendingActions);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidSummary.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidTrending.StatusCode);
    }

    private static async Task<SeedResult> SeedDashboardAsync(
        PulsePilotApiFactory factory,
        AuthenticationResponse owner)
    {
        var now = DateTimeOffset.UtcNow;
        var cluster = FeedbackCluster.Create(
            owner.WorkspaceId,
            "Checkout payment failures",
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            now.AddDays(-9));
        cluster.UpdatePriority(91m, FeedbackPriority.P1, now.AddMinutes(-30));
        var completed = CreateFeedback(owner, "Checkout fails", now.AddHours(-1));
        var failed = CreateFeedback(owner, "Card rejected", now.AddHours(-2));
        var previous = CreateFeedback(owner, "Older checkout failure", now.AddDays(-8));
        var completedLease = completed.StartProcessing(now.AddMinutes(-59));
        completed.CompleteProcessing(completedLease, now.AddMinutes(-58));
        completed.AssignToCluster(cluster.Id, now.AddMinutes(-57));
        var failedLease = failed.StartProcessing(now.AddMinutes(-119));
        failed.FailProcessing(failedLease, now.AddMinutes(-118));
        failed.AssignToCluster(cluster.Id, now.AddMinutes(-117));
        previous.AssignToCluster(cluster.Id, now.AddDays(-8).AddMinutes(1));
        var analysis = FeedbackAnalysis.Create(
            owner.WorkspaceId,
            completed.Id,
            FeedbackCategory.Bug,
            FeedbackComponent.Payments,
            severity: 5,
            FeedbackSentiment.Negative,
            "Checkout is rejecting valid payment cards.",
            "Investigate the checkout payment flow.",
            confidence: 0.98m,
            now.AddMinutes(-58));
        var pendingAction = PendingAction.Create(
            owner.WorkspaceId,
            completed.Id,
            cluster.Id,
            PendingActionType.CreateEngineeringIssue,
            "[P1] Checkout payment failures",
            "Create an engineering issue for the growing checkout failure cluster.",
            "{\"priority\":\"p1\"}",
            now.AddMinutes(-30));

        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IFeedbackClusterRepository>()
            .AddAsync(cluster);
        var feedbackRepository = scope.ServiceProvider.GetRequiredService<IFeedbackRepository>();
        await feedbackRepository.AddAsync(completed);
        await feedbackRepository.AddAsync(failed);
        await feedbackRepository.AddAsync(previous);
        await scope.ServiceProvider.GetRequiredService<IFeedbackAnalysisRepository>()
            .AddAsync(analysis);
        await scope.ServiceProvider.GetRequiredService<IPendingActionRepository>()
            .AddAsync(pendingAction);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        return new SeedResult(cluster.Id, previous.Id, pendingAction.Id);
    }

    private static FeedbackEntity CreateFeedback(
        AuthenticationResponse owner,
        string title,
        DateTimeOffset createdAt)
    {
        return FeedbackEntity.Create(
            owner.WorkspaceId,
            owner.UserId,
            title,
            $"{title} content",
            FeedbackSource.Manual,
            null,
            null,
            createdAt);
    }

    private static async Task<AuthenticatedClient> CreateAuthenticatedClientAsync(
        PulsePilotApiFactory factory,
        string label)
    {
        var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterCommand(
                $"{label}-{Guid.CreateVersion7():N}@example.com",
                $"{label} owner",
                "correct-horse-battery-staple",
                $"{label} workspace"),
            SerializerOptions);
        response.EnsureSuccessStatusCode();
        var authentication = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(
            SerializerOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            authentication!.AccessToken);

        return new AuthenticatedClient(client, authentication);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }

    private sealed record AuthenticatedClient(
        HttpClient Client,
        AuthenticationResponse Authentication);

    private sealed record SeedResult(
        Guid ClusterId,
        Guid PreviousFeedbackId,
        Guid PendingActionId);
}
