using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Application;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.AI;
using PulsePilot.Domain.Feedback;
using PulsePilot.Infrastructure;

namespace PulsePilot.Evaluation;

public interface IEvaluationProvider : IAsyncDisposable
{
    string Name { get; }

    string Model { get; }

    bool IsModelEvaluation { get; }

    Task<FeedbackAnalysisResult> AnalyzeAsync(
        EvaluationCase evaluationCase,
        CancellationToken cancellationToken);
}

public sealed class ReplayEvaluationProvider : IEvaluationProvider
{
    public string Name => "replay";

    public string Model => "golden-expectation-replay";

    public bool IsModelEvaluation => false;

    public Task<FeedbackAnalysisResult> AnalyzeAsync(
        EvaluationCase evaluationCase,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evaluationCase);
        cancellationToken.ThrowIfCancellationRequested();
        var expected = evaluationCase.Expected;
        var result = new FeedbackAnalysisResult(
            Enum.Parse<FeedbackCategory>(expected.Category.Preferred),
            Enum.Parse<FeedbackComponent>(expected.Component.Preferred),
            expected.Severity.Minimum,
            Enum.Parse<FeedbackSentiment>(expected.Sentiment.Preferred),
            string.Join("; ", expected.RequiredSummaryConcepts),
            string.Join("; ", expected.RequiredActionConcepts),
            Math.Max(expected.MinimumConfidence, 0.95m));

        return Task.FromResult(result);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

public sealed class OpenAiEvaluationProvider : IEvaluationProvider
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly ILLMClient _client;

    private OpenAiEvaluationProvider(
        ServiceProvider serviceProvider,
        IServiceScope scope,
        ILLMClient client,
        string model)
    {
        _serviceProvider = serviceProvider;
        _scope = scope;
        _client = client;
        Model = model;
    }

    public string Name => "openai";

    public string Model { get; }

    public bool IsModelEvaluation => true;

    public static OpenAiEvaluationProvider Create(RunnerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OPENAI_API_KEY is required for real provider evaluation.");
        }

        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] =
                "Host=unused;Database=unused;Username=unused;Password=unused",
            ["OpenAI:Enabled"] = "true",
            ["OpenAI:ApiKey"] = apiKey,
            ["OpenAI:Model"] = options.Model,
            ["OpenAI:Endpoint"] = options.Endpoint.AbsoluteUri,
            ["OpenAI:NetworkTimeoutSeconds"] = options.CaseTimeoutSeconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = false,
            ValidateScopes = true,
        });
        var scope = serviceProvider.CreateScope();

        return new OpenAiEvaluationProvider(
            serviceProvider,
            scope,
            scope.ServiceProvider.GetRequiredService<ILLMClient>(),
            options.Model);
    }

    public Task<FeedbackAnalysisResult> AnalyzeAsync(
        EvaluationCase evaluationCase,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evaluationCase);
        var request = new FeedbackAnalysisRequest(
            CreateDeterministicId(evaluationCase.Id),
            evaluationCase.Input.Title,
            evaluationCase.Input.Content,
            Enum.Parse<FeedbackSource>(evaluationCase.Input.Source));

        return _client.AnalyzeFeedbackAsync(request, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _scope.Dispose();
        await _serviceProvider.DisposeAsync();
    }

    private static Guid CreateDeterministicId(string id)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(id));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
