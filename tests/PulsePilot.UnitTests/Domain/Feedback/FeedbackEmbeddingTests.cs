using PulsePilot.Domain.Common;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.UnitTests.Domain.Feedback;

public sealed class FeedbackEmbeddingTests
{
    [Fact]
    public void Create_AcceptsValidatedEmbeddingAndDefensivelyCopiesValues()
    {
        var values = CreateValues(0.25f);
        var embeddedAt = DateTimeOffset.UtcNow;

        var embedding = FeedbackEmbedding.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            values,
            "text-embedding-3-small",
            new string('a', FeedbackEmbedding.SourceHashLength),
            embeddedAt);
        values[0] = 0.75f;

        Assert.Equal(FeedbackEmbedding.Dimensions, embedding.Values.Count);
        Assert.Equal(0.25f, embedding.Values[0]);
        Assert.Equal("text-embedding-3-small", embedding.Model);
        Assert.Equal(new string('a', FeedbackEmbedding.SourceHashLength), embedding.SourceHash);
        Assert.Equal(embeddedAt, embedding.CreatedAt);
    }

    [Fact]
    public void ReplaceResult_PreservesIdentityAndUpdatesSourceMetadata()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var embedding = FeedbackEmbedding.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            CreateValues(0.25f),
            "first-model",
            new string('a', FeedbackEmbedding.SourceHashLength),
            createdAt);

        embedding.ReplaceResult(
            CreateValues(0.5f),
            "second-model",
            new string('b', FeedbackEmbedding.SourceHashLength),
            createdAt.AddMinutes(1));

        Assert.Equal(0.5f, embedding.Values[0]);
        Assert.Equal("second-model", embedding.Model);
        Assert.Equal(new string('b', FeedbackEmbedding.SourceHashLength), embedding.SourceHash);
        Assert.Equal(createdAt.AddMinutes(1), embedding.UpdatedAt);
    }

    [Theory]
    [InlineData(1_535)]
    [InlineData(1_537)]
    public void Create_RejectsUnexpectedDimensions(int dimensions)
    {
        var values = Enumerable.Repeat(0.1f, dimensions).ToArray();

        Assert.Throws<DomainException>(() => FeedbackEmbedding.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            values,
            "test-model",
            new string('a', FeedbackEmbedding.SourceHashLength),
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_RejectsZeroOrNonFiniteVectors()
    {
        var zeroValues = new float[FeedbackEmbedding.Dimensions];
        var nonFiniteValues = CreateValues(0.1f);
        nonFiniteValues[10] = float.NaN;

        Assert.Throws<DomainException>(() => Create(zeroValues));
        Assert.Throws<DomainException>(() => Create(nonFiniteValues));
    }

    private static FeedbackEmbedding Create(float[] values)
    {
        return FeedbackEmbedding.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            values,
            "test-model",
            new string('a', FeedbackEmbedding.SourceHashLength),
            DateTimeOffset.UtcNow);
    }

    private static float[] CreateValues(float firstValue)
    {
        var values = new float[FeedbackEmbedding.Dimensions];
        values[0] = firstValue;

        return values;
    }
}
