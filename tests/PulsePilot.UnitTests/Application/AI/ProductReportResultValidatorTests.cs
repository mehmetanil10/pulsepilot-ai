using FluentValidation;
using PulsePilot.Application.AI;

namespace PulsePilot.UnitTests.Application.AI;

public sealed class ProductReportResultValidatorTests
{
    private readonly ProductReportResultValidator _validator = new();

    [Fact]
    public async Task Validator_AcceptsBoundedStructuredReport()
    {
        var result = new ProductReportResult(
            "Weekly Product Intelligence Report",
            "Payment failures were the highest-priority issue this week.",
            ["Payment failures increased from 2 to 7 reports."],
            ["Investigate the P1 payment cluster first."]);

        var validationResult = await _validator.ValidateAsync(result);

        Assert.True(validationResult.IsValid);
    }

    [Fact]
    public async Task Validator_RejectsEmptyDuplicateAndOversizedOutput()
    {
        var duplicate = new string('x', ProductReportResult.MaxListItemLength + 1);
        var result = new ProductReportResult(
            string.Empty,
            string.Empty,
            [duplicate, duplicate],
            []);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _validator.ValidateAndThrowAsync(result));

        Assert.Contains(exception.Errors, failure =>
            failure.PropertyName == nameof(ProductReportResult.Title));
        Assert.Contains(exception.Errors, failure =>
            failure.PropertyName == nameof(ProductReportResult.ExecutiveSummary));
        Assert.Contains(exception.Errors, failure =>
            failure.PropertyName == nameof(ProductReportResult.KeyInsights)
                && failure.ErrorMessage.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(exception.Errors, failure =>
            failure.PropertyName == "KeyInsights[0]");
        Assert.Contains(exception.Errors, failure =>
            failure.PropertyName == nameof(ProductReportResult.RecommendedEngineeringPriorities));
    }
}
