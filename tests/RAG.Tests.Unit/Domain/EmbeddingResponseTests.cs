using RAG.Core.Domain;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Domain;

public class EmbeddingResponseTests
{
    [Fact]
    public void Validate_WithValidEmbeddings_DoesNotThrow()
    {
        // Arrange
        var embeddings = new List<float[]>
        {
            new float[384],
            new float[384],
            new float[384]
        };
        var response = new EmbeddingResponse(embeddings);

        // Act & Assert
        Should.NotThrow(() => response.Validate(expectedCount: 3, expectedDimension: 384));
    }

    [Fact]
    public void Validate_WithNullEmbeddingsList_ThrowsArgumentNullException()
    {
        // Arrange
        var response = new EmbeddingResponse(null!);

        // Act & Assert
        var exception = Should.Throw<ArgumentNullException>(() => response.Validate(expectedCount: 3));
        exception.ParamName.ShouldBe("Embeddings");
    }

    [Fact]
    public void Validate_WithMismatchedCount_ThrowsInvalidOperationException()
    {
        // Arrange
        var embeddings = new List<float[]>
        {
            new float[384],
            new float[384]
        };
        var response = new EmbeddingResponse(embeddings);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => response.Validate(expectedCount: 3));
        exception.Message.ShouldContain("Embedding count mismatch");
        exception.Message.ShouldContain("expected 3, got 2");
    }

    [Fact]
    public void Validate_WithNullEmbeddingInList_ThrowsInvalidOperationException()
    {
        // Arrange
        var embeddings = new List<float[]>
        {
            new float[384],
            null!,
            new float[384]
        };
        var response = new EmbeddingResponse(embeddings);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => response.Validate(expectedCount: 3));
        exception.Message.ShouldContain("Embedding at index 1 is null");
    }

    [Fact]
    public void Validate_WithInvalidDimension_ThrowsInvalidOperationException()
    {
        // Arrange
        var embeddings = new List<float[]>
        {
            new float[384],
            new float[256], // Wrong dimension
            new float[384]
        };
        var response = new EmbeddingResponse(embeddings);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => response.Validate(expectedCount: 3, expectedDimension: 384));
        exception.Message.ShouldContain("invalid dimension");
        exception.Message.ShouldContain("expected 384, got 256");
    }

    [Fact]
    public void Validate_WithCustomDimension_ValidatesCorrectly()
    {
        // Arrange
        var embeddings = new List<float[]>
        {
            new float[512],
            new float[512]
        };
        var response = new EmbeddingResponse(embeddings);

        // Act & Assert
        Should.NotThrow(() => response.Validate(expectedCount: 2, expectedDimension: 512));
    }
}
