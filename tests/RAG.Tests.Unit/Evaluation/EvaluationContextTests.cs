using RAG.Core.Domain;
using RAG.Evaluation.Models;
using Shouldly;

namespace RAG.Tests.Unit.Evaluation;

public class EvaluationContextTests
{
    [Fact]
    public void IsValid_WithQuery_ReturnsTrue()
    {
        // Arrange
        var context = new EvaluationContext
        {
            Query = "What is machine learning?",
            Response = "Machine learning is..."
        };

        // Act & Assert
        context.IsValid().ShouldBeTrue();
    }

    [Fact]
    public void IsValid_WithEmptyQuery_ReturnsFalse()
    {
        // Arrange
        var context = new EvaluationContext
        {
            Query = "",
            Response = "Some response"
        };

        // Act & Assert
        context.IsValid().ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithWhitespaceQuery_ReturnsFalse()
    {
        // Arrange
        var context = new EvaluationContext
        {
            Query = "   ",
            Response = "Some response"
        };

        // Act & Assert
        context.IsValid().ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithAllProperties_SetsValuesCorrectly()
    {
        // Arrange
        var relevantIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var retrievedDocs = new List<RetrievalResult>
        {
            new(Guid.NewGuid(), 0.95, "Document text", "doc1.pdf")
        };
        var parameters = new Dictionary<string, object> { ["k"] = 10 };

        // Act
        var context = new EvaluationContext
        {
            Query = "Test query",
            Response = "Test response",
            GroundTruth = "Expected answer",
            RelevantDocumentIds = relevantIds,
            RetrievedDocuments = retrievedDocs,
            Parameters = parameters,
            SampleId = "sample-001"
        };

        // Assert
        context.Query.ShouldBe("Test query");
        context.Response.ShouldBe("Test response");
        context.GroundTruth.ShouldBe("Expected answer");
        context.RelevantDocumentIds.Count.ShouldBe(2);
        context.RetrievedDocuments.Count.ShouldBe(1);
        context.Parameters["k"].ShouldBe(10);
        context.SampleId.ShouldBe("sample-001");
    }

    [Fact]
    public void DefaultCollections_AreEmpty()
    {
        // Arrange & Act
        var context = new EvaluationContext
        {
            Query = "Test",
            Response = "Response"
        };

        // Assert
        context.RelevantDocumentIds.ShouldBeEmpty();
        context.RetrievedDocuments.ShouldBeEmpty();
        context.Parameters.ShouldBeEmpty();
    }
}
