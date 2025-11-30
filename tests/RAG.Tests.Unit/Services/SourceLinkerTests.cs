using Microsoft.Extensions.Logging;
using Moq;
using RAG.Application.Services;
using RAG.Core.Domain;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Services;

public class SourceLinkerTests
{
    private readonly Mock<ILogger<SourceLinker>> _mockLogger;
    private readonly SourceLinker _sourceLinker;

    public SourceLinkerTests()
    {
        _mockLogger = new Mock<ILogger<SourceLinker>>();
        _sourceLinker = new SourceLinker(_mockLogger.Object);
    }

    [Fact]
    public void LinkSources_WithValidCitations_MapsCorrectly()
    {
        // Arrange
        var response = "The capital is Paris [Source 1]. Population is 2 million [Source 2].";
        var retrievalResults = new List<RetrievalResult>
        {
            new(Guid.NewGuid(), 0.95, "Paris is the capital of France", "france.pdf"),
            new(Guid.NewGuid(), 0.88, "Paris has a population of 2 million", "population.pdf")
        };

        // Act
        var linkedSources = _sourceLinker.LinkSources(response, retrievalResults);

        // Assert
        linkedSources.ShouldNotBeNull();
        linkedSources.Count.ShouldBe(2);

        linkedSources[0].SourceId.ShouldBe(retrievalResults[0].DocumentId);
        linkedSources[0].Title.ShouldBe("france.pdf");
        linkedSources[0].Score.ShouldBe(0.95);

        linkedSources[1].SourceId.ShouldBe(retrievalResults[1].DocumentId);
        linkedSources[1].Title.ShouldBe("population.pdf");
        linkedSources[1].Score.ShouldBe(0.88);
    }

    [Fact]
    public void LinkSources_WithOutOfRangeCitation_HandlesGracefully()
    {
        // Arrange
        var response = "Data from [Source 1] and [Source 5].";
        var retrievalResults = new List<RetrievalResult>
        {
            new(Guid.NewGuid(), 0.9, "Sample text", "doc1.pdf")
        };

        // Act
        var linkedSources = _sourceLinker.LinkSources(response, retrievalResults);

        // Assert
        linkedSources.ShouldNotBeNull();
        linkedSources.Count.ShouldBe(1); // Only Source 1 is valid
        linkedSources[0].SourceId.ShouldBe(retrievalResults[0].DocumentId);
    }

    [Fact]
    public void LinkSources_WithNoCitations_ReturnsEmptyList()
    {
        // Arrange
        var response = "This is a response without any citations.";
        var retrievalResults = new List<RetrievalResult>
        {
            new(Guid.NewGuid(), 0.9, "Sample text", "doc1.pdf")
        };

        // Act
        var linkedSources = _sourceLinker.LinkSources(response, retrievalResults);

        // Assert
        linkedSources.ShouldNotBeNull();
        linkedSources.ShouldBeEmpty();
    }

    [Fact]
    public void LinkSources_WithEmptyResponse_ReturnsEmptyList()
    {
        // Arrange
        var response = "";
        var retrievalResults = new List<RetrievalResult>
        {
            new(Guid.NewGuid(), 0.9, "Sample text", "doc1.pdf")
        };

        // Act
        var linkedSources = _sourceLinker.LinkSources(response, retrievalResults);

        // Assert
        linkedSources.ShouldNotBeNull();
        linkedSources.ShouldBeEmpty();
    }

    [Fact]
    public void LinkSources_WithDuplicateCitations_ReturnsUniqueLinks()
    {
        // Arrange
        var response = "According to [Source 1], data shows trends. " +
                      "Furthermore, [Source 1] also mentions patterns.";
        var retrievalResults = new List<RetrievalResult>
        {
            new(Guid.NewGuid(), 0.9, "Data trends and patterns", "data.pdf")
        };

        // Act
        var linkedSources = _sourceLinker.LinkSources(response, retrievalResults);

        // Assert
        linkedSources.ShouldNotBeNull();
        linkedSources.Count.ShouldBe(1); // Unique sources only
        linkedSources[0].SourceId.ShouldBe(retrievalResults[0].DocumentId);
    }

    [Fact]
    public void LinkSources_WithCaseInsensitiveCitations_WorksCorrectly()
    {
        // Arrange
        var response = "Data from [source 1] and [SOURCE 2].";
        var retrievalResults = new List<RetrievalResult>
        {
            new(Guid.NewGuid(), 0.95, "Text 1", "doc1.pdf"),
            new(Guid.NewGuid(), 0.88, "Text 2", "doc2.pdf")
        };

        // Act
        var linkedSources = _sourceLinker.LinkSources(response, retrievalResults);

        // Assert
        linkedSources.ShouldNotBeNull();
        linkedSources.Count.ShouldBe(2);
        linkedSources[0].Title.ShouldBe("doc1.pdf");
        linkedSources[1].Title.ShouldBe("doc2.pdf");
    }

    [Fact]
    public void LinkSources_WithNullRetrievalResults_ThrowsArgumentNullException()
    {
        // Arrange
        var response = "Some response [Source 1]";

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            _sourceLinker.LinkSources(response, null!));
    }

    [Fact]
    public void LinkSources_TruncatesLongExcerpts()
    {
        // Arrange
        var response = "Data from [Source 1].";
        var longText = new string('a', 500); // 500 characters
        var retrievalResults = new List<RetrievalResult>
        {
            new(Guid.NewGuid(), 0.9, longText, "doc1.pdf")
        };

        // Act
        var linkedSources = _sourceLinker.LinkSources(response, retrievalResults);

        // Assert
        linkedSources.ShouldNotBeNull();
        linkedSources.Count.ShouldBe(1);
        linkedSources[0].Excerpt.Length.ShouldBeLessThanOrEqualTo(203); // 200 + "..."
        linkedSources[0].Excerpt.ShouldEndWith("...");
    }

    [Fact]
    public void LinkSources_WithShortExcerpt_DoesNotTruncate()
    {
        // Arrange
        var response = "Data from [Source 1].";
        var shortText = "Short text";
        var retrievalResults = new List<RetrievalResult>
        {
            new(Guid.NewGuid(), 0.9, shortText, "doc1.pdf")
        };

        // Act
        var linkedSources = _sourceLinker.LinkSources(response, retrievalResults);

        // Assert
        linkedSources.ShouldNotBeNull();
        linkedSources.Count.ShouldBe(1);
        linkedSources[0].Excerpt.ShouldBe(shortText);
        linkedSources[0].Excerpt.ShouldNotEndWith("...");
    }

    [Fact]
    public void LinkSources_WithMultipleValidCitations_OrdersCorrectly()
    {
        // Arrange
        var response = "Data from [Source 3], [Source 1], and [Source 2].";
        var retrievalResults = new List<RetrievalResult>
        {
            new(Guid.NewGuid(), 0.95, "Text 1", "doc1.pdf"),
            new(Guid.NewGuid(), 0.88, "Text 2", "doc2.pdf"),
            new(Guid.NewGuid(), 0.82, "Text 3", "doc3.pdf")
        };

        // Act
        var linkedSources = _sourceLinker.LinkSources(response, retrievalResults);

        // Assert
        linkedSources.ShouldNotBeNull();
        linkedSources.Count.ShouldBe(3);

        // Should be ordered by source number (1, 2, 3)
        linkedSources[0].Title.ShouldBe("doc1.pdf");
        linkedSources[1].Title.ShouldBe("doc2.pdf");
        linkedSources[2].Title.ShouldBe("doc3.pdf");
    }

    [Fact]
    public void LinkSources_WithSpacingVariations_ParsesCorrectly()
    {
        // Arrange
        var response = "Data from [Source  1] and [Source   2].";
        var retrievalResults = new List<RetrievalResult>
        {
            new(Guid.NewGuid(), 0.95, "Text 1", "doc1.pdf"),
            new(Guid.NewGuid(), 0.88, "Text 2", "doc2.pdf")
        };

        // Act
        var linkedSources = _sourceLinker.LinkSources(response, retrievalResults);

        // Assert
        linkedSources.ShouldNotBeNull();
        linkedSources.Count.ShouldBe(2);
        linkedSources[0].Title.ShouldBe("doc1.pdf");
        linkedSources[1].Title.ShouldBe("doc2.pdf");
    }

    [Fact]
    public void LinkSources_WithZeroIndexCitation_IgnoresIt()
    {
        // Arrange
        var response = "Data from [Source 0] and [Source 1].";
        var retrievalResults = new List<RetrievalResult>
        {
            new(Guid.NewGuid(), 0.95, "Text 1", "doc1.pdf")
        };

        // Act
        var linkedSources = _sourceLinker.LinkSources(response, retrievalResults);

        // Assert
        linkedSources.ShouldNotBeNull();
        linkedSources.Count.ShouldBe(1); // Only Source 1 is valid (0 is out of range)
        linkedSources[0].Title.ShouldBe("doc1.pdf");
    }

    [Fact]
    public void LinkSources_WithNegativeIndexCitation_IgnoresIt()
    {
        // Arrange
        var response = "Data from [Source -1] and [Source 1].";
        var retrievalResults = new List<RetrievalResult>
        {
            new(Guid.NewGuid(), 0.95, "Text 1", "doc1.pdf")
        };

        // Act
        var linkedSources = _sourceLinker.LinkSources(response, retrievalResults);

        // Assert
        linkedSources.ShouldNotBeNull();
        linkedSources.Count.ShouldBe(1); // Only Source 1 is valid
        linkedSources[0].Title.ShouldBe("doc1.pdf");
    }

    [Fact]
    public void LinkSources_PreservesScoreFromRetrievalResult()
    {
        // Arrange
        var response = "Data from [Source 1].";
        var expectedScore = 0.9876;
        var retrievalResults = new List<RetrievalResult>
        {
            new(Guid.NewGuid(), expectedScore, "Text", "doc1.pdf")
        };

        // Act
        var linkedSources = _sourceLinker.LinkSources(response, retrievalResults);

        // Assert
        linkedSources.ShouldNotBeNull();
        linkedSources.Count.ShouldBe(1);
        linkedSources[0].Score.ShouldBe(expectedScore);
    }
}
