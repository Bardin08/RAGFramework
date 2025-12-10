using RAG.Evaluation.Models;
using Xunit;

namespace RAG.Tests.Unit.Evaluation.GroundTruth;

public class GroundTruthEntryTests
{
    [Fact]
    public void GroundTruthEntry_WithAnswerAliases_GetAllValidAnswers_ReturnsAll()
    {
        // Arrange
        var entry = new GroundTruthEntry(
            Query: "What is the capital of France?",
            ExpectedAnswer: "Paris",
            RelevantDocumentIds: new[] { "doc-1" })
        {
            AnswerAliases = new[] { "City of Light", "Paris, France", "Paree" }
        };

        // Act
        var allAnswers = entry.GetAllValidAnswers().ToList();

        // Assert
        // Returns ExpectedAnswer + all aliases that don't match ExpectedAnswer case-insensitively
        Assert.Equal(4, allAnswers.Count);
        Assert.Contains("Paris", allAnswers);
        Assert.Contains("City of Light", allAnswers);
        Assert.Contains("Paris, France", allAnswers);
        Assert.Contains("Paree", allAnswers);
    }

    [Fact]
    public void GroundTruthEntry_WithoutAnswerAliases_GetAllValidAnswers_ReturnsOnlyExpectedAnswer()
    {
        // Arrange
        var entry = new GroundTruthEntry(
            Query: "What is 2+2?",
            ExpectedAnswer: "4",
            RelevantDocumentIds: new[] { "doc-1" });

        // Act
        var allAnswers = entry.GetAllValidAnswers().ToList();

        // Assert
        Assert.Single(allAnswers);
        Assert.Contains("4", allAnswers);
    }

    [Fact]
    public void GroundTruthEntry_GetAllValidAnswers_SkipsDuplicates()
    {
        // Arrange - Alias that matches expected answer (case-insensitive)
        var entry = new GroundTruthEntry(
            Query: "Test?",
            ExpectedAnswer: "Paris",
            RelevantDocumentIds: new[] { "doc-1" })
        {
            AnswerAliases = new[] { "paris", "PARIS", "Paris City" }
        };

        // Act
        var allAnswers = entry.GetAllValidAnswers().ToList();

        // Assert
        // Should skip "paris" and "PARIS" as they match "Paris" case-insensitively
        Assert.Contains("Paris", allAnswers);
        Assert.Contains("Paris City", allAnswers);
        Assert.DoesNotContain("PARIS", allAnswers); // Skipped as duplicate
    }

    [Fact]
    public void GroundTruthEntry_GetAllValidAnswers_SkipsWhitespace()
    {
        // Arrange
        var entry = new GroundTruthEntry(
            Query: "Test?",
            ExpectedAnswer: "Answer",
            RelevantDocumentIds: new[] { "doc-1" })
        {
            AnswerAliases = new[] { "Valid Alias", "", "   ", "Another Alias" }
        };

        // Act
        var allAnswers = entry.GetAllValidAnswers().ToList();

        // Assert
        Assert.Equal(3, allAnswers.Count);
        Assert.Contains("Answer", allAnswers);
        Assert.Contains("Valid Alias", allAnswers);
        Assert.Contains("Another Alias", allAnswers);
    }

    [Fact]
    public void GroundTruthEntry_WithMetadata_StoresMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["questionId"] = "q1",
            ["questionSource"] = "Web",
            ["normalizedAliases"] = new List<string> { "paris", "paris, france" }
        };

        var entry = new GroundTruthEntry(
            Query: "What is the capital of France?",
            ExpectedAnswer: "Paris",
            RelevantDocumentIds: new[] { "doc-1" })
        {
            Metadata = metadata
        };

        // Assert
        Assert.NotNull(entry.Metadata);
        Assert.Equal(3, entry.Metadata.Count);
        Assert.Equal("q1", entry.Metadata["questionId"]);
        Assert.Equal("Web", entry.Metadata["questionSource"]);
    }

    [Fact]
    public void GroundTruthEntry_IsValid_WithValidData_ReturnsTrue()
    {
        // Arrange
        var entry = new GroundTruthEntry(
            Query: "Valid question?",
            ExpectedAnswer: "Valid answer",
            RelevantDocumentIds: new[] { "doc-1", "doc-2" });

        // Act & Assert
        Assert.True(entry.IsValid());
    }

    [Fact]
    public void GroundTruthEntry_IsValid_WithEmptyQuery_ReturnsFalse()
    {
        // Arrange
        var entry = new GroundTruthEntry(
            Query: "",
            ExpectedAnswer: "Answer",
            RelevantDocumentIds: new[] { "doc-1" });

        // Act & Assert
        Assert.False(entry.IsValid());
    }

    [Fact]
    public void GroundTruthEntry_IsValid_WithNoRelevantDocs_ReturnsFalse()
    {
        // Arrange
        var entry = new GroundTruthEntry(
            Query: "Question?",
            ExpectedAnswer: "Answer",
            RelevantDocumentIds: Array.Empty<string>());

        // Act & Assert
        Assert.False(entry.IsValid());
    }

    [Fact]
    public void GroundTruthEntry_RecordEquality_WorksCorrectly()
    {
        // Arrange - use same array reference for equality comparison
        // Note: Records with collection properties use reference equality for the collections
        var relevantDocs = new[] { "doc-1" };

        var entry1 = new GroundTruthEntry(
            Query: "Question?",
            ExpectedAnswer: "Answer",
            RelevantDocumentIds: relevantDocs);

        var entry2 = new GroundTruthEntry(
            Query: "Question?",
            ExpectedAnswer: "Answer",
            RelevantDocumentIds: relevantDocs);

        var entry3 = new GroundTruthEntry(
            Query: "Different?",
            ExpectedAnswer: "Answer",
            RelevantDocumentIds: relevantDocs);

        // Act & Assert
        Assert.Equal(entry1, entry2);
        Assert.NotEqual(entry1, entry3);
    }

    [Fact]
    public void GroundTruthEntry_WithExpression_CreatesNewInstance()
    {
        // Arrange
        var original = new GroundTruthEntry(
            Query: "Question?",
            ExpectedAnswer: "Answer",
            RelevantDocumentIds: new[] { "doc-1" });

        // Act
        var modified = original with
        {
            AnswerAliases = new[] { "Alias1", "Alias2" }
        };

        // Assert
        Assert.NotSame(original, modified);
        Assert.Null(original.AnswerAliases);
        Assert.NotNull(modified.AnswerAliases);
        Assert.Equal(2, modified.AnswerAliases.Count);
    }
}
