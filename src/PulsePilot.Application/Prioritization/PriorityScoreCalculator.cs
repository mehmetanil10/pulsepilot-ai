using Microsoft.Extensions.Options;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Prioritization;

public sealed class PriorityScoreCalculator(IOptions<PriorityScoringOptions> options)
    : IPriorityScoreCalculator
{
    private readonly PriorityScoringOptions _options = options.Value;

    public PriorityScoringResult Calculate(
        IReadOnlyCollection<PriorityScoringMember> members,
        DateTimeOffset calculatedAt)
    {
        ArgumentNullException.ThrowIfNull(members);

        if (members.Count == 0)
        {
            throw new ArgumentException(
                "At least one feedback member is required to calculate priority.",
                nameof(members));
        }

        if (calculatedAt == default)
        {
            throw new ArgumentException(
                "Priority calculation timestamp is required.",
                nameof(calculatedAt));
        }

        var normalizedCalculatedAt = calculatedAt.ToUniversalTime();
        ValidateMembers(members);

        var maximumSeverity = members.Max(member => member.Severity ?? 0);

        if (maximumSeverity == 0)
        {
            throw new ArgumentException(
                "At least one feedback member must have a severity.",
                nameof(members));
        }

        var severityFactor = maximumSeverity / (decimal)FeedbackAnalysis.MaximumSeverity;
        var frequencyFactor = NormalizeCount(
            members.Count,
            _options.FrequencyNormalizationCount);
        var impactedCustomerCount = members
            .Select(CreateCustomerIdentity)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var customerImpactFactor = NormalizeCount(
            impactedCustomerCount,
            _options.CustomerImpactNormalizationCount);
        var recentSince = normalizedCalculatedAt.AddDays(-_options.RecencyWindowDays);
        var recentFeedbackCount = members.Count(member =>
            member.CreatedAt.ToUniversalTime() >= recentSince
            && member.CreatedAt.ToUniversalTime() <= normalizedCalculatedAt);
        var recencyFactor = recentFeedbackCount / (decimal)members.Count;
        var score = decimal.Round(
            100m * (
                severityFactor * _options.SeverityWeight
                + frequencyFactor * _options.FrequencyWeight
                + customerImpactFactor * _options.CustomerImpactWeight
                + recencyFactor * _options.RecencyWeight),
            2,
            MidpointRounding.AwayFromZero);

        return new PriorityScoringResult(
            score,
            GetPriority(score),
            severityFactor,
            frequencyFactor,
            customerImpactFactor,
            recencyFactor);
    }

    private FeedbackPriority GetPriority(decimal score)
    {
        if (score >= _options.P1Threshold)
        {
            return FeedbackPriority.P1;
        }

        if (score >= _options.P2Threshold)
        {
            return FeedbackPriority.P2;
        }

        return score >= _options.P3Threshold
            ? FeedbackPriority.P3
            : FeedbackPriority.P4;
    }

    private static decimal NormalizeCount(int count, int normalizationCount)
    {
        return Math.Min(count / (decimal)normalizationCount, 1m);
    }

    private static string CreateCustomerIdentity(PriorityScoringMember member)
    {
        if (!string.IsNullOrWhiteSpace(member.CustomerEmail))
        {
            return $"email:{member.CustomerEmail.Trim().ToUpperInvariant()}";
        }

        if (!string.IsNullOrWhiteSpace(member.CustomerName))
        {
            return $"name:{member.CustomerName.Trim().ToUpperInvariant()}";
        }

        return $"anonymous:{member.FeedbackId:N}";
    }

    private static void ValidateMembers(IEnumerable<PriorityScoringMember> members)
    {
        foreach (var member in members)
        {
            if (member.FeedbackId == Guid.Empty)
            {
                throw new ArgumentException("Feedback member id is required.", nameof(members));
            }

            if (member.CreatedAt == default)
            {
                throw new ArgumentException(
                    "Feedback member creation timestamp is required.",
                    nameof(members));
            }

            if (member.Severity is < FeedbackAnalysis.MinimumSeverity
                or > FeedbackAnalysis.MaximumSeverity)
            {
                throw new ArgumentException(
                    $"Feedback severity must be between {FeedbackAnalysis.MinimumSeverity} and {FeedbackAnalysis.MaximumSeverity}.",
                    nameof(members));
            }
        }
    }
}
