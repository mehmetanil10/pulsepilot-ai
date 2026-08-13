using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PulsePilot.Application;
using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Agents;
using PulsePilot.Application.Copilot;

namespace PulsePilot.UnitTests.Application.Copilot;

public sealed class CopilotChatTests
{
    [Fact]
    public async Task Validator_EnforcesConfiguredMessageBoundary()
    {
        var validator = new CopilotChatCommandValidator(
            Options.Create(new AgentOrchestrationOptions
            {
                MaxUserMessageLength = 10,
            }));

        var emptyResult = await validator.ValidateAsync(
            new CopilotChatCommand("   "));
        var oversizedResult = await validator.ValidateAsync(
            new CopilotChatCommand(new string('x', 11)));
        var validResult = await validator.ValidateAsync(
            new CopilotChatCommand("Priorities"));

        Assert.False(emptyResult.IsValid);
        Assert.False(oversizedResult.IsValid);
        Assert.True(validResult.IsValid);
    }

    [Fact]
    public async Task Service_UsesTrustedWorkspaceAndHidesProviderCallIdentifiers()
    {
        var workspaceId = Guid.CreateVersion7();
        var orchestrator = new RecordingAgentOrchestrator(new AgentOrchestrationResult(
            "Payments are the highest-priority issue.",
            2,
            [
                new AgentToolUsage(
                    "provider-call-id-must-remain-internal",
                    AgentToolNames.GetTrendingIssues,
                    true),
            ]));
        var services = new ServiceCollection();
        services.AddApplication();
        services.RemoveAll<IAgentOrchestrator>();
        services.AddSingleton<IAgentOrchestrator>(orchestrator);
        services.AddSingleton<ICurrentUserContext>(new StaticCurrentUserContext(
            workspaceId));
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICopilotChatService>();

        var response = await service.ChatAsync(new CopilotChatCommand(
            "What should engineering prioritize?"));

        Assert.Equal(workspaceId, orchestrator.WorkspaceId);
        Assert.Equal(
            "What should engineering prioritize?",
            orchestrator.UserMessage);
        Assert.Equal("Payments are the highest-priority issue.", response.Answer);
        Assert.Equal(2, response.ModelTurnCount);
        Assert.Equal(1, response.ToolCallCount);
        var usage = Assert.Single(response.ToolUsages);
        Assert.Equal(AgentToolNames.GetTrendingIssues, usage.ToolName);
        Assert.True(usage.Succeeded);
        Assert.DoesNotContain(
            response.GetType().GetProperties(),
            property => property.Name.Contains("CallId", StringComparison.Ordinal));
        Assert.DoesNotContain(
            usage.GetType().GetProperties(),
            property => property.Name.Contains("CallId", StringComparison.Ordinal));
    }

    private sealed class RecordingAgentOrchestrator(AgentOrchestrationResult result)
        : IAgentOrchestrator
    {
        public Guid WorkspaceId { get; private set; }

        public string? UserMessage { get; private set; }

        public Task<AgentOrchestrationResult> RunAsync(
            Guid workspaceId,
            string userMessage,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WorkspaceId = workspaceId;
            UserMessage = userMessage;

            return Task.FromResult(result);
        }
    }

    private sealed class StaticCurrentUserContext(Guid workspaceId)
        : ICurrentUserContext
    {
        public Guid UserId { get; } = Guid.CreateVersion7();

        public Guid WorkspaceId { get; } = workspaceId;

        public string Role => "Admin";
    }
}
