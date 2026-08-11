using PulsePilot.Domain.Common;

namespace PulsePilot.Domain.Feedback;

public sealed class FeedbackEmbedding : AuditableEntity
{
    public const int Dimensions = 1_536;
    public const int MaxModelLength = 100;
    public const int SourceHashLength = 64;

    private float[] _values = null!;

    private FeedbackEmbedding()
    {
    }

    private FeedbackEmbedding(
        Guid id,
        Guid workspaceId,
        Guid feedbackId,
        IReadOnlyList<float> values,
        string model,
        string sourceHash,
        DateTimeOffset embeddedAt)
        : base(id, embeddedAt)
    {
        WorkspaceId = Guard.NotEmpty(workspaceId, nameof(workspaceId));
        FeedbackId = Guard.NotEmpty(feedbackId, nameof(feedbackId));
        SetResult(values, model, sourceHash);
    }

    public Guid WorkspaceId { get; private set; }

    public Guid FeedbackId { get; private set; }

    public IReadOnlyList<float> Values => _values;

    public string Model { get; private set; } = null!;

    public string SourceHash { get; private set; } = null!;

    public static FeedbackEmbedding Create(
        Guid workspaceId,
        Guid feedbackId,
        IReadOnlyList<float> values,
        string model,
        string sourceHash,
        DateTimeOffset embeddedAt)
    {
        return new FeedbackEmbedding(
            Guid.CreateVersion7(),
            workspaceId,
            feedbackId,
            values,
            model,
            sourceHash,
            embeddedAt);
    }

    public void ReplaceResult(
        IReadOnlyList<float> values,
        string model,
        string sourceHash,
        DateTimeOffset embeddedAt)
    {
        var validated = ValidateResult(values, model, sourceHash);

        MarkUpdated(embeddedAt);
        ApplyResult(validated);
    }

    private void SetResult(
        IReadOnlyList<float> values,
        string model,
        string sourceHash)
    {
        ApplyResult(ValidateResult(values, model, sourceHash));
    }

    private void ApplyResult(ValidatedEmbedding result)
    {
        _values = result.Values;
        Model = result.Model;
        SourceHash = result.SourceHash;
    }

    private static ValidatedEmbedding ValidateResult(
        IReadOnlyList<float> values,
        string model,
        string sourceHash)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count != Dimensions)
        {
            throw new DomainException($"Embedding must contain exactly {Dimensions} values.");
        }

        var copiedValues = new float[Dimensions];
        var hasNonZeroValue = false;

        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];

            if (!float.IsFinite(value))
            {
                throw new DomainException("Embedding values must be finite numbers.");
            }

            copiedValues[index] = value;
            hasNonZeroValue |= value != 0;
        }

        if (!hasNonZeroValue)
        {
            throw new DomainException("Embedding must contain at least one non-zero value.");
        }

        var validatedModel = Guard.RequiredText(model, nameof(model), MaxModelLength);
        var validatedSourceHash = Guard.RequiredText(
            sourceHash,
            nameof(sourceHash),
            SourceHashLength);

        if (validatedSourceHash.Length != SourceHashLength
            || validatedSourceHash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new DomainException(
                $"Source hash must be a {SourceHashLength}-character hexadecimal value.");
        }

        return new ValidatedEmbedding(
            copiedValues,
            validatedModel,
            validatedSourceHash.ToLowerInvariant());
    }

    private sealed record ValidatedEmbedding(
        float[] Values,
        string Model,
        string SourceHash);
}
