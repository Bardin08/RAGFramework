using RAG.Core.Domain;
using Shouldly;

namespace RAG.Tests.Unit.Domain;

public class GenerationRequestTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesGenerationRequest()
    {
        // Arrange
        var query = "What is RAG?";
        var retrievedDocs = new List<RetrievalResult>
        {
            new RetrievalResult(Guid.NewGuid(), 0.95f, "RAG is...", "source1"),
            new RetrievalResult(Guid.NewGuid(), 0.85f, "RAG stands for...", "source2")
        };
        var maxTokens = 500;
        var temperature = 0.7f;

        // Act
        var request = new GenerationRequest(query, retrievedDocs, maxTokens, temperature);

        // Assert
        request.ShouldNotBeNull();
        request.Query.ShouldBe(query);
        request.RetrievedDocuments.ShouldBe(retrievedDocs);
        request.MaxTokens.ShouldBe(maxTokens);
        request.Temperature.ShouldBe(temperature);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithInvalidQuery_ThrowsArgumentException(string invalidQuery)
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new GenerationRequest(invalidQuery, new List<RetrievalResult>(), 500, 0.7f))
            .Message.ShouldContain("Query cannot be empty");
    }

    [Fact]
    public void Constructor_WithNullRetrievedDocuments_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new GenerationRequest("query", null!, 500, 0.7f));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithInvalidMaxTokens_ThrowsArgumentException(int invalidMaxTokens)
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new GenerationRequest("query", new List<RetrievalResult>(), invalidMaxTokens, 0.7f))
            .Message.ShouldContain("MaxTokens must be greater than 0");
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(2.1f)]
    [InlineData(3.0f)]
    public void Constructor_WithInvalidTemperature_ThrowsArgumentException(float invalidTemperature)
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() =>
            new GenerationRequest("query", new List<RetrievalResult>(), 500, invalidTemperature))
            .Message.ShouldContain("Temperature must be between 0.0 and 2.0");
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    [InlineData(2.0f)]
    public void Constructor_WithValidTemperature_CreatesGenerationRequest(float temperature)
    {
        // Arrange & Act
        var request = new GenerationRequest("query", new List<RetrievalResult>(), 500, temperature);

        // Assert
        request.Temperature.ShouldBe(temperature);
    }

    [Fact]
    public void Constructor_WithEmptyRetrievedDocuments_CreatesGenerationRequest()
    {
        // Arrange & Act
        var request = new GenerationRequest("query", new List<RetrievalResult>(), 500, 0.7f);

        // Assert
        request.RetrievedDocuments.ShouldNotBeNull();
        request.RetrievedDocuments.ShouldBeEmpty();
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var docs = new List<RetrievalResult> { new RetrievalResult(Guid.NewGuid(), 0.9f, "text", "source") };
        var request1 = new GenerationRequest("query", docs, 500, 0.7f);
        var request2 = new GenerationRequest("query", docs, 500, 0.7f);

        // Act & Assert
        request1.ShouldBe(request2);
    }
}
