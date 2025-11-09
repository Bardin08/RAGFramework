using RAG.Core.Domain;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Domain;

public class RetrievalResultTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesRetrievalResult()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var score = 0.95f;
        var text = "Retrieved text snippet";
        var source = "https://example.com";

        // Act
        var result = new RetrievalResult(documentId, score, text, source);

        // Assert
        result.ShouldNotBeNull();
        result.DocumentId.ShouldBe(documentId);
        result.Score.ShouldBe(score);
        result.Text.ShouldBe(text);
        result.Source.ShouldBe(source);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var result1 = new RetrievalResult(documentId, 0.95f, "text", "source");
        var result2 = new RetrievalResult(documentId, 0.95f, "text", "source");

        // Act & Assert
        result1.ShouldBe(result2);
        (result1 == result2).ShouldBeTrue();
    }

    [Fact]
    public void RecordEquality_WithDifferentValues_AreNotEqual()
    {
        // Arrange
        var result1 = new RetrievalResult(Guid.NewGuid(), 0.95f, "text1", "source");
        var result2 = new RetrievalResult(Guid.NewGuid(), 0.95f, "text2", "source");

        // Act & Assert
        result1.ShouldNotBe(result2);
        (result1 != result2).ShouldBeTrue();
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    public void Constructor_WithDifferentScores_CreatesRetrievalResult(float score)
    {
        // Arrange & Act
        var result = new RetrievalResult(Guid.NewGuid(), score, "text", "source");

        // Assert
        result.Score.ShouldBe(score);
    }

    [Fact]
    public void Constructor_WithEmptyStrings_CreatesRetrievalResult()
    {
        // Arrange & Act
        var result = new RetrievalResult(Guid.NewGuid(), 0.5f, "", "");

        // Assert
        result.ShouldNotBeNull();
        result.Text.ShouldBeEmpty();
        result.Source.ShouldBeEmpty();
    }
}
