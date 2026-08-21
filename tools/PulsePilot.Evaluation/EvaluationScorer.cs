using System.Globalization;
using System.Text;
using PulsePilot.Application.AI;

namespace PulsePilot.Evaluation;

public sealed class EvaluationScorer
{
    public EvaluationCaseMetrics Score(
        EvaluationCase evaluationCase,
        FeedbackAnalysisResult actual)
    {
        ArgumentNullException.ThrowIfNull(evaluationCase);
        ArgumentNullException.ThrowIfNull(actual);

        var category = actual.Category.ToString();
        var component = actual.Component.ToString();
        var sentiment = actual.Sentiment.ToString();
        var strictCategory = category == evaluationCase.Expected.Category.Preferred;
        var tolerantCategory = evaluationCase.Expected.Category.Accepted.Contains(category);
        var strictComponent = component == evaluationCase.Expected.Component.Preferred;
        var tolerantComponent = evaluationCase.Expected.Component.Accepted.Contains(component);
        var strictSentiment = sentiment == evaluationCase.Expected.Sentiment.Preferred;
        var tolerantSentiment = evaluationCase.Expected.Sentiment.Accepted.Contains(sentiment);
        var severityWithinRange = actual.Severity
            >= evaluationCase.Expected.Severity.Minimum
            && actual.Severity <= evaluationCase.Expected.Severity.Maximum;
        var summaryRecall = CalculateConceptRecall(
            actual.Summary,
            evaluationCase.Expected.RequiredSummaryConcepts);
        var actionRecall = CalculateConceptRecall(
            actual.SuggestedAction,
            evaluationCase.Expected.RequiredActionConcepts);
        var confidenceMeetsFloor = actual.Confidence
            >= evaluationCase.Expected.MinimumConfidence;

        return new EvaluationCaseMetrics(
            ContractValid: true,
            strictCategory,
            tolerantCategory,
            strictComponent,
            tolerantComponent,
            strictSentiment,
            tolerantSentiment,
            severityWithinRange,
            summaryRecall,
            actionRecall,
            confidenceMeetsFloor,
            StrictPass: strictCategory
                && strictComponent
                && strictSentiment
                && severityWithinRange
                && summaryRecall == 1m
                && actionRecall == 1m
                && confidenceMeetsFloor,
            TolerantPass: tolerantCategory
                && tolerantComponent
                && tolerantSentiment
                && severityWithinRange
                && summaryRecall == 1m
                && actionRecall == 1m
                && confidenceMeetsFloor);
    }

    public static EvaluationCaseMetrics FailedMetrics()
    {
        return new EvaluationCaseMetrics(
            ContractValid: false,
            StrictCategoryMatch: false,
            TolerantCategoryMatch: false,
            StrictComponentMatch: false,
            TolerantComponentMatch: false,
            StrictSentimentMatch: false,
            TolerantSentimentMatch: false,
            SeverityWithinRange: false,
            SummaryConceptRecall: 0m,
            ActionConceptRecall: 0m,
            ConfidenceMeetsFloor: false,
            StrictPass: false,
            TolerantPass: false);
    }

    private static decimal CalculateConceptRecall(
        string text,
        IReadOnlyCollection<string> requiredConcepts)
    {
        if (requiredConcepts.Count == 0)
        {
            return 1m;
        }

        var normalizedText = Normalize(text);
        var matches = requiredConcepts.Count(concept =>
            normalizedText.Contains(Normalize(concept), StringComparison.Ordinal));

        return decimal.Round(
            (decimal)matches / requiredConcepts.Count,
            4,
            MidpointRounding.AwayFromZero);
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSeparator = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character)
                == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append(' ');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().Trim();
    }
}
