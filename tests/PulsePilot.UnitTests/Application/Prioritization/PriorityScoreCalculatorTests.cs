using Microsoft.Extensions.Options;
using PulsePilot.Application.Prioritization;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.UnitTests.Application.Prioritization;

public sealed class PriorityScoreCalculatorTests
{
    [Fact]
    public void Calculate_CombinesNormalizedFactorsUsingConfiguredWeights()
    {
        var calculatedAt = DateTimeOffset.UtcNow;
        var members = Enumerable.Range(1, 10)
            .Select(index => new PriorityScoringMember(
                Guid.CreateVersion7(),
                null,
                $"customer-{index}@example.com",
                index <= 5 ? calculatedAt.AddDays(-1) : calculatedAt.AddDays(-30),
                index == 1 ? 5 : 3))
            .ToList();
        var calculator = CreateCalculator(new PriorityScoringOptions());

        var result = calculator.Calculate(members, calculatedAt);

        Assert.Equal(77.50m, result.Score);
        Assert.Equal(FeedbackPriority.P1, result.Priority);
        Assert.Equal(1m, result.SeverityFactor);
        Assert.Equal(0.5m, result.FrequencyFactor);
        Assert.Equal(1m, result.CustomerImpactFactor);
        Assert.Equal(0.5m, result.RecencyFactor);
    }

    [Fact]
    public void Calculate_DeduplicatesKnownCustomersCaseInsensitively()
    {
        var calculatedAt = DateTimeOffset.UtcNow;
        var members = new[]
        {
            new PriorityScoringMember(
                Guid.CreateVersion7(),
                null,
                "customer@example.com",
                calculatedAt,
                3),
            new PriorityScoringMember(
                Guid.CreateVersion7(),
                null,
                " CUSTOMER@example.com ",
                calculatedAt,
                3),
        };
        var calculator = CreateCalculator(new PriorityScoringOptions
        {
            SeverityWeight = 0,
            FrequencyWeight = 0,
            CustomerImpactWeight = 1,
            RecencyWeight = 0,
            CustomerImpactNormalizationCount = 2,
        });

        var result = calculator.Calculate(members, calculatedAt);

        Assert.Equal(50m, result.Score);
        Assert.Equal(FeedbackPriority.P2, result.Priority);
        Assert.Equal(0.5m, result.CustomerImpactFactor);
    }

    [Theory]
    [InlineData(75, FeedbackPriority.P1)]
    [InlineData(50, FeedbackPriority.P2)]
    [InlineData(25, FeedbackPriority.P3)]
    [InlineData(24, FeedbackPriority.P4)]
    public void Calculate_UsesInclusiveConfiguredPriorityThresholds(
        int severityPercentage,
        FeedbackPriority expectedPriority)
    {
        var calculatedAt = DateTimeOffset.UtcNow;
        var calculator = CreateCalculator(new PriorityScoringOptions
        {
            SeverityWeight = 0,
            FrequencyWeight = 1,
            CustomerImpactWeight = 0,
            RecencyWeight = 0,
            FrequencyNormalizationCount = 100,
        });
        var members = Enumerable.Range(0, severityPercentage)
            .Select(_ => new PriorityScoringMember(
                Guid.NewGuid(),
                null,
                null,
                calculatedAt,
                1))
            .ToList();

        var result = calculator.Calculate(members, calculatedAt);

        Assert.Equal(expectedPriority, result.Priority);
    }

    [Fact]
    public void Calculate_RejectsMembersWithoutAnySeverity()
    {
        var calculatedAt = DateTimeOffset.UtcNow;
        var calculator = CreateCalculator(new PriorityScoringOptions());
        var members = new[]
        {
            new PriorityScoringMember(
                Guid.CreateVersion7(),
                null,
                null,
                calculatedAt,
                null),
        };

        Assert.Throws<ArgumentException>(() => calculator.Calculate(members, calculatedAt));
    }

    private static PriorityScoreCalculator CreateCalculator(PriorityScoringOptions options)
    {
        return new PriorityScoreCalculator(Options.Create(options));
    }
}
