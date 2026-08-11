using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulsePilot.Application;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.AI;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Application.FeedbackProcessing;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;
using PulsePilot.Infrastructure.Persistence;
using PulsePilot.IntegrationTests.Persistence;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.IntegrationTests.AI;

public sealed class FeedbackAnalysisProcessorTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly FeedbackAnalysisResult FirstResult = new(
        FeedbackCategory.Bug,
        FeedbackComponent.Payments,
        4,
        FeedbackSentiment.Negative,
        "Payment card creation fails.",
        "Inspect payment tokenization and add a regression test.",
        0.94m);

    private static readonly FeedbackAnalysisResult UpdatedResult = new(
        FeedbackCategory.Complaint,
        FeedbackComponent.Performance,
        3,
        FeedbackSentiment.Negative,
        "The dashboard is consistently slow.",
        "Profile dashboard queries and rendering time.",
        0.89m);

    [Fact]
    public async Task Processor_PersistsAndIdempotentlyReplacesAnalysis()
    {
        var feedback = await SeedFeedbackAsync("processor-success");
        var fakeClient = new SequenceLlmClient(
            _ => Task.FromResult(FirstResult),
            _ => Task.FromResult(UpdatedResult));
        await using var serviceProvider = CreateProcessorProvider(fakeClient);

        var firstProcessing = await ProcessNextAsync(serviceProvider);

        Assert.Equal(FeedbackAnalysisProcessStatus.Succeeded, firstProcessing.Status);
        Assert.Equal(1, firstProcessing.Attempts);

        Guid analysisId;

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var persistedFeedback = await dbContext.Feedback
                .SingleAsync(entity => entity.Id == feedback.Id);
            var persistedAnalysis = await dbContext.FeedbackAnalyses
                .SingleAsync(analysis => analysis.FeedbackId == feedback.Id);

            Assert.Equal(ProcessingStatus.Completed, persistedFeedback.ProcessingStatus);
            Assert.Null(persistedFeedback.ProcessingLeaseId);
            Assert.Equal(FirstResult.Summary, persistedAnalysis.Summary);
            analysisId = persistedAnalysis.Id;

            persistedFeedback.UpdateDetails(
                "Dashboard is slow",
                "The dashboard takes more than ten seconds to load.",
                FeedbackSource.Manual,
                null,
                null,
                DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync();
        }

        var secondProcessing = await ProcessNextAsync(serviceProvider);

        Assert.Equal(FeedbackAnalysisProcessStatus.Succeeded, secondProcessing.Status);

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var persistedFeedback = await dbContext.Feedback
                .SingleAsync(entity => entity.Id == feedback.Id);
            var persistedAnalysis = await dbContext.FeedbackAnalyses
                .SingleAsync(analysis => analysis.FeedbackId == feedback.Id);

            Assert.Equal(ProcessingStatus.Completed, persistedFeedback.ProcessingStatus);
            Assert.Equal(analysisId, persistedAnalysis.Id);
            Assert.Equal(UpdatedResult.Category, persistedAnalysis.Category);
            Assert.Equal(UpdatedResult.Component, persistedAnalysis.Component);
            Assert.Equal(UpdatedResult.Summary, persistedAnalysis.Summary);
            Assert.Equal(2, fakeClient.CallCount);
        }
    }

    [Fact]
    public async Task Processor_RetriesTransientFailureAndCompletes()
    {
        var feedback = await SeedFeedbackAsync("processor-retry");
        var fakeClient = new SequenceLlmClient(
            _ => throw new LlmProviderException(
                LlmProviderFailureKind.ProviderUnavailable,
                "The test provider is temporarily unavailable.",
                isTransient: true),
            _ => throw new LlmProviderException(
                LlmProviderFailureKind.ProviderUnavailable,
                "The test provider is temporarily unavailable.",
                isTransient: true),
            _ => Task.FromResult(FirstResult));
        await using var serviceProvider = CreateProcessorProvider(fakeClient);

        var result = await ProcessNextAsync(serviceProvider);

        Assert.Equal(FeedbackAnalysisProcessStatus.Succeeded, result.Status);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(3, fakeClient.CallCount);

        await using var scope = serviceProvider.CreateAsyncScope();
        var persistedFeedback = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Feedback
            .SingleAsync(entity => entity.Id == feedback.Id);

        Assert.Equal(ProcessingStatus.Completed, persistedFeedback.ProcessingStatus);
    }

    [Fact]
    public async Task Processor_DoesNotRetryPermanentFailureAndMarksFailed()
    {
        var feedback = await SeedFeedbackAsync("processor-failure");
        var fakeClient = new SequenceLlmClient(
            _ => throw new LlmProviderException(
                LlmProviderFailureKind.Refused,
                "The test provider refused the request."));
        await using var serviceProvider = CreateProcessorProvider(fakeClient);

        var result = await ProcessNextAsync(serviceProvider);

        Assert.Equal(FeedbackAnalysisProcessStatus.Failed, result.Status);
        Assert.Equal(LlmProviderFailureKind.Refused, result.FailureKind);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(1, fakeClient.CallCount);

        await using var scope = serviceProvider.CreateAsyncScope();
        var persistedFeedback = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Feedback
            .SingleAsync(entity => entity.Id == feedback.Id);

        Assert.Equal(ProcessingStatus.Failed, persistedFeedback.ProcessingStatus);
        Assert.Null(persistedFeedback.ProcessingLeaseId);
        Assert.Empty(await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .FeedbackAnalyses
            .Where(analysis => analysis.FeedbackId == feedback.Id)
            .ToListAsync());
    }

    [Fact]
    public async Task Processor_DiscardsLateResultAfterLeaseRecovery()
    {
        var feedback = await SeedFeedbackAsync("processor-expired-lease");
        var fakeClient = new BlockingLlmClient();
        await using var serviceProvider = CreateProcessorProvider(fakeClient);

        var processingTask = ProcessNextAsync(serviceProvider);
        await fakeClient.Started;

        await using (var recoveryProvider = database.CreateServiceProvider())
        await using (var recoveryScope = recoveryProvider.CreateAsyncScope())
        {
            var recoveredAt = DateTimeOffset.UtcNow;
            var recoveredCount = await recoveryScope.ServiceProvider
                .GetRequiredService<IFeedbackProcessingQueue>()
                .RecoverStaleAsync(
                    recoveredAt.AddMinutes(2),
                    recoveredAt,
                    maxCount: 10);

            Assert.Equal(1, recoveredCount);
        }

        fakeClient.Complete(FirstResult);
        var result = await processingTask;

        Assert.Equal(FeedbackAnalysisProcessStatus.Abandoned, result.Status);

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedFeedback = await dbContext.Feedback
            .SingleAsync(entity => entity.Id == feedback.Id);

        Assert.Equal(ProcessingStatus.Pending, persistedFeedback.ProcessingStatus);
        Assert.Empty(await dbContext.FeedbackAnalyses
            .Where(analysis => analysis.FeedbackId == feedback.Id)
            .ToListAsync());

        var processingLeaseId = persistedFeedback.StartProcessing(DateTimeOffset.UtcNow);
        persistedFeedback.FailProcessing(processingLeaseId, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Processor_RetriesTimeoutAndMarksFailedAfterMaximumAttempts()
    {
        var feedback = await SeedFeedbackAsync("processor-timeout");
        var fakeClient = new TimeoutLlmClient();
        await using var serviceProvider = CreateProcessorProvider(
            fakeClient,
            options => options.AnalysisTimeoutSeconds = 0);

        var result = await ProcessNextAsync(serviceProvider);

        Assert.Equal(FeedbackAnalysisProcessStatus.Failed, result.Status);
        Assert.Equal(LlmProviderFailureKind.ProviderUnavailable, result.FailureKind);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(3, fakeClient.CallCount);

        await using var scope = serviceProvider.CreateAsyncScope();
        var persistedFeedback = await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Feedback
            .SingleAsync(entity => entity.Id == feedback.Id);

        Assert.Equal(ProcessingStatus.Failed, persistedFeedback.ProcessingStatus);
    }

    private ServiceProvider CreateProcessorProvider(
        ILLMClient llmClient,
        Action<FeedbackProcessingOptions>? configureOptions = null)
    {
        return database.CreateServiceProvider(
            configureServices: services =>
            {
                services.AddApplication();
                services.RemoveAll<ILLMClient>();
                services.AddSingleton(llmClient);
                services.Configure<FeedbackProcessingOptions>(options =>
                {
                    options.MaxAttempts = 3;
                    options.AnalysisTimeoutSeconds = 5;
                    options.BaseRetryDelayMilliseconds = 0;
                    options.MaxRetryDelaySeconds = 1;
                    options.RetryJitterFactor = 0;
                    configureOptions?.Invoke(options);
                });
            });
    }

    private static async Task<FeedbackAnalysisProcessResult> ProcessNextAsync(
        ServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        return await scope.ServiceProvider
            .GetRequiredService<IFeedbackAnalysisProcessor>()
            .ProcessNextAsync();
    }

    private async Task<FeedbackEntity> SeedFeedbackAsync(string label)
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Create(
            $"{label}-{Guid.CreateVersion7():N}@example.com",
            "Processor Owner",
            "password-hash",
            now);
        var workspace = Workspace.Create($"{label} workspace", now);
        var membership = WorkspaceMember.Join(
            workspace.Id,
            user.Id,
            WorkspaceRole.Admin,
            now);
        var feedback = FeedbackEntity.Create(
            workspace.Id,
            user.Id,
            "Card cannot be added",
            "After the latest update I cannot add my credit card.",
            FeedbackSource.Manual,
            null,
            null,
            now);

        await using var serviceProvider = database.CreateServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUserRepository>().AddAsync(user);
        await scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>().AddAsync(workspace);
        await scope.ServiceProvider
            .GetRequiredService<IWorkspaceMemberRepository>()
            .AddAsync(membership);
        await scope.ServiceProvider.GetRequiredService<IFeedbackRepository>().AddAsync(feedback);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        return feedback;
    }

    private sealed class SequenceLlmClient(
        params Func<FeedbackAnalysisRequest, Task<FeedbackAnalysisResult>>[] responses)
        : ILLMClient
    {
        private int _callCount;

        public int CallCount => _callCount;

        public Task<FeedbackAnalysisResult> AnalyzeFeedbackAsync(
            FeedbackAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var index = Interlocked.Increment(ref _callCount) - 1;

            if (index >= responses.Length)
            {
                throw new InvalidOperationException("No fake LLM response is configured.");
            }

            return responses[index](request);
        }
    }

    private sealed class BlockingLlmClient : ILLMClient
    {
        private readonly TaskCompletionSource _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<FeedbackAnalysisResult> _result =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public async Task<FeedbackAnalysisResult> AnalyzeFeedbackAsync(
            FeedbackAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            _started.TrySetResult();

            return await _result.Task.WaitAsync(cancellationToken);
        }

        public void Complete(FeedbackAnalysisResult result)
        {
            _result.TrySetResult(result);
        }
    }

    private sealed class TimeoutLlmClient : ILLMClient
    {
        private int _callCount;

        public int CallCount => _callCount;

        public async Task<FeedbackAnalysisResult> AnalyzeFeedbackAsync(
            FeedbackAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            throw new InvalidOperationException("The timeout test should always be cancelled.");
        }
    }
}
