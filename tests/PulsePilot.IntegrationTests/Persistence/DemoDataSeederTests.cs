using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Actions;
using PulsePilot.Application.Authentication;
using PulsePilot.Application.Backlog;
using PulsePilot.Application.Dashboard;
using PulsePilot.Application.Feedback;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Workspaces;
using PulsePilot.Infrastructure.Persistence;
using PulsePilot.Infrastructure.Persistence.Seeding;
using PulsePilot.IntegrationTests.Api;

namespace PulsePilot.IntegrationTests.Persistence;

public sealed class DemoDataSeederTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    [Fact]
    public async Task SeedAsync_CreatesUsableDemoWorkspaceAndIsIdempotent()
    {
        var email = $"demo-{Guid.CreateVersion7():N}@example.com";
        const string password = "demo-password-for-integration-tests";
        var configuration = new Dictionary<string, string?>
        {
            ["Seed:Run"] = "true",
            ["Seed:Email"] = email,
            ["Seed:Password"] = password,
            ["Seed:DisplayName"] = "Integration Demo Owner",
            ["Seed:WorkspaceName"] = "Integration Demo Workspace",
            ["Seed:FeedbackCount"] = "100",
        };

        DemoSeedResult firstResult;

        await using (var serviceProvider = database.CreateServiceProvider(configuration))
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            firstResult = await scope.ServiceProvider
                .GetRequiredService<IDemoDataSeeder>()
                .SeedAsync();
        }

        DemoSeedResult secondResult;

        await using (var serviceProvider = database.CreateServiceProvider(configuration))
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            secondResult = await scope.ServiceProvider
                .GetRequiredService<IDemoDataSeeder>()
                .SeedAsync();
        }

        Assert.Equal(100, firstResult.AddedFeedbackCount);
        Assert.Equal(100, firstResult.TotalFeedbackCount);
        Assert.Equal(firstResult.UserId, secondResult.UserId);
        Assert.Equal(firstResult.WorkspaceId, secondResult.WorkspaceId);
        Assert.Equal(0, secondResult.AddedFeedbackCount);
        Assert.Equal(100, secondResult.TotalFeedbackCount);

        await using (var serviceProvider = database.CreateServiceProvider(configuration))
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var user = await dbContext.Users.SingleAsync(
                candidate => candidate.Id == firstResult.UserId);
            var membership = await dbContext.WorkspaceMembers.SingleAsync(
                candidate => candidate.UserId == firstResult.UserId
                    && candidate.WorkspaceId == firstResult.WorkspaceId);
            var feedback = await dbContext.Feedback
                .Where(candidate => candidate.WorkspaceId == firstResult.WorkspaceId)
                .ToListAsync();
            var analyses = await dbContext.FeedbackAnalyses
                .Where(candidate => candidate.WorkspaceId == firstResult.WorkspaceId)
                .ToListAsync();
            var clusters = await dbContext.FeedbackClusters
                .Where(candidate => candidate.WorkspaceId == firstResult.WorkspaceId)
                .ToListAsync();
            var persistedActions = await dbContext.PendingActions
                .Where(candidate => candidate.WorkspaceId == firstResult.WorkspaceId)
                .ToListAsync();

            Assert.True(user.IsActive);
            Assert.Equal(WorkspaceRole.Admin, membership.Role);
            Assert.Equal(
                PasswordVerificationStatus.Success,
                passwordHasher.VerifyPassword(user.PasswordHash, password));
            Assert.Equal(100, feedback.Count);
            Assert.All(Enum.GetValues<FeedbackSource>(), source =>
                Assert.Contains(feedback, item => item.Source == source));
            Assert.True(feedback.Count(item =>
                item.Title?.StartsWith("Payments:", StringComparison.Ordinal) == true) >= 12);
            Assert.Equal(84, analyses.Count);
            Assert.Equal(84, feedback.Count(item =>
                item.ProcessingStatus == ProcessingStatus.Completed));
            Assert.Equal(6, feedback.Count(item =>
                item.ProcessingStatus == ProcessingStatus.Failed));
            Assert.Equal(10, feedback.Count(item =>
                item.ProcessingStatus == ProcessingStatus.Pending));
            Assert.Equal(42, feedback.Count(item => item.FeedbackClusterId.HasValue));
            Assert.Equal(7, clusters.Count);
            Assert.Equal(2, clusters.Count(item => item.Priority == FeedbackPriority.P1));
            Assert.Equal(5, persistedActions.Count);
            Assert.Equal(2, persistedActions.Count(
                item => item.Status == PendingActionStatus.Pending));
            Assert.Equal(2, persistedActions.Count(
                item => item.Status == PendingActionStatus.Executed));
            Assert.Single(
                persistedActions,
                item => item.Status == PendingActionStatus.Rejected);
            Assert.Single(dbContext.BacklogItems.Where(
                item => item.WorkspaceId == firstResult.WorkspaceId));
            Assert.Single(dbContext.CustomerResponseDrafts.Where(
                item => item.WorkspaceId == firstResult.WorkspaceId));
        }

        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginCommand(email, password));
        var authentication = await loginResponse.Content
            .ReadFromJsonAsync<AuthenticationResponse>();

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(authentication);
        Assert.Equal(firstResult.UserId, authentication.UserId);
        Assert.Equal(firstResult.WorkspaceId, authentication.WorkspaceId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            authentication.AccessToken);
        using var feedbackResponse = await client.GetAsync("/api/feedback?page=1&pageSize=20");
        var feedbackList = await feedbackResponse.Content
            .ReadFromJsonAsync<FeedbackListResponse>(SerializerOptions);

        Assert.Equal(HttpStatusCode.OK, feedbackResponse.StatusCode);
        Assert.NotNull(feedbackList);
        Assert.Equal(100, feedbackList.TotalCount);
        Assert.Equal(20, feedbackList.Items.Count);

        using var dashboardResponse = await client.GetAsync(
            "/api/dashboard/summary?periodDays=30");
        var dashboard = await dashboardResponse.Content
            .ReadFromJsonAsync<DashboardSummaryResponse>(SerializerOptions);

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.NotNull(dashboard);
        Assert.Equal(84, dashboard.Kpis.AiProcessed);
        Assert.Equal(2, dashboard.Kpis.CriticalIssues);
        Assert.Equal(2, dashboard.Kpis.PendingActions);
        Assert.Equal(6, dashboard.Kpis.ProcessingFailures);
        Assert.NotEmpty(dashboard.Categories);

        using var trendingResponse = await client.GetAsync(
            "/api/dashboard/trending?periodDays=7&limit=10");
        var trending = await trendingResponse.Content
            .ReadFromJsonAsync<DashboardTrendingResponse>(SerializerOptions);

        Assert.Equal(HttpStatusCode.OK, trendingResponse.StatusCode);
        Assert.NotNull(trending);
        Assert.NotEmpty(trending.Items);
        Assert.All(trending.Items, item => Assert.True(item.DeltaCount > 0));

        using var actionsResponse = await client.GetAsync(
            "/api/actions?page=1&pageSize=20");
        var actions = await actionsResponse.Content
            .ReadFromJsonAsync<PendingActionListResponse>(SerializerOptions);

        Assert.Equal(HttpStatusCode.OK, actionsResponse.StatusCode);
        Assert.NotNull(actions);
        Assert.Equal(5, actions.TotalCount);
        Assert.Contains(actions.Items, item => item.Status == PendingActionStatus.Pending);
        Assert.Contains(actions.Items, item => item.Status == PendingActionStatus.Executed);

        using var backlogResponse = await client.GetAsync(
            "/api/backlog?page=1&pageSize=20");
        var backlog = await backlogResponse.Content
            .ReadFromJsonAsync<BacklogItemListResponse>(SerializerOptions);

        Assert.Equal(HttpStatusCode.OK, backlogResponse.StatusCode);
        Assert.NotNull(backlog);
        Assert.Equal(1, backlog.TotalCount);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));

        return options;
    }
}
