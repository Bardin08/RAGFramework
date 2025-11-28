using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RAG.Application.Reranking;
using RAG.Core.Configuration;
using RAG.Core.Domain;
using Shouldly;

namespace RAG.Tests.Unit.Reranking;

/// <summary>
/// Unit tests for RRFReranker (Reciprocal Rank Fusion reranking algorithm).
/// Tests verify RRF formula correctness, configuration validation, edge cases, and sorting behavior.
/// </summary>
public class RRFRerankerTests
{
    private readonly Mock<ILogger<RRFReranker>> _loggerMock;
    private readonly RRFConfig _defaultConfig;

    public RRFRerankerTests()
    {
        _loggerMock = new Mock<ILogger<RRFReranker>>();
        _defaultConfig = new RRFConfig { K = 60 };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new RRFReranker(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var config = Options.Create(_defaultConfig);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new RRFReranker(config, null!));
    }

    [Fact]
    public void Constructor_WithInvalidConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidConfig = new RRFConfig { K = 0 }; // Invalid: K must be > 0
        var config = Options.Create(invalidConfig);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            new RRFReranker(config, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithValidConfig_Succeeds()
    {
        // Arrange
        var config = Options.Create(_defaultConfig);

        // Act
        var reranker = new RRFReranker(config, _loggerMock.Object);

        // Assert
        reranker.ShouldNotBeNull();
    }

    #endregion

    #region RRF Calculation Tests

    [Fact]
    public void Rerank_WithTwoResultSets_CalculatesCorrectRRFScores()
    {
        // Arrange
        var config = Options.Create(_defaultConfig);
        var reranker = new RRFReranker(config, _loggerMock.Object);

        var doc1 = Guid.NewGuid();
        var doc2 = Guid.NewGuid();
        var doc3 = Guid.NewGuid();

        // BM25 results: doc1 (rank 1), doc2 (rank 2)
        var bm25Results = new List<RetrievalResult>
        {
            new(doc1, 0.9, "text1", "source1"),
            new(doc2, 0.8, "text2", "source2")
        };

        // Dense results: doc1 (rank 2), doc3 (rank 1)
        var denseResults = new List<RetrievalResult>
        {
            new(doc3, 0.95, "text3", "source3"),
            new(doc1, 0.85, "text1", "source1")
        };

        var resultSets = new List<List<RetrievalResult>> { bm25Results, denseResults };

        // Act
        var reranked = reranker.Rerank(resultSets, 10);

        // Assert
        reranked.ShouldNotBeNull();
        reranked.Count.ShouldBe(3); // 3 unique documents

        // Expected RRF scores (k=60):
        // doc1: 1/(60+1) + 1/(60+2) = 1/61 + 1/62 ≈ 0.01639 + 0.01613 = 0.03252
        // doc2: 1/(60+2) = 1/62 ≈ 0.01613
        // doc3: 1/(60+1) = 1/61 ≈ 0.01639

        // doc1 should be first (highest combined RRF score)
        reranked[0].DocumentId.ShouldBe(doc1);
        reranked[0].Score.ShouldBe(0.03252, 0.0001);

        // doc3 should be second
        reranked[1].DocumentId.ShouldBe(doc3);
        reranked[1].Score.ShouldBe(0.01639, 0.0001);

        // doc2 should be third
        reranked[2].DocumentId.ShouldBe(doc2);
        reranked[2].Score.ShouldBe(0.01613, 0.0001);
    }

    [Fact]
    public void Rerank_WithSingleResultSet_CalculatesCorrectRRFScores()
    {
        // Arrange
        var config = Options.Create(_defaultConfig);
        var reranker = new RRFReranker(config, _loggerMock.Object);

        var doc1 = Guid.NewGuid();
        var doc2 = Guid.NewGuid();

        var results = new List<RetrievalResult>
        {
            new(doc1, 0.9, "text1", "source1"), // Rank 1
            new(doc2, 0.8, "text2", "source2")  // Rank 2
        };

        var resultSets = new List<List<RetrievalResult>> { results };

        // Act
        var reranked = reranker.Rerank(resultSets, 10);

        // Assert
        reranked.Count.ShouldBe(2);

        // Expected RRF scores (k=60):
        // doc1: 1/(60+1) = 1/61 ≈ 0.01639
        // doc2: 1/(60+2) = 1/62 ≈ 0.01613

        reranked[0].DocumentId.ShouldBe(doc1);
        reranked[0].Score.ShouldBe(0.01639, 0.0001);

        reranked[1].DocumentId.ShouldBe(doc2);
        reranked[1].Score.ShouldBe(0.01613, 0.0001);
    }

    [Fact]
    public void Rerank_VerifiesRRFFormula_WithKnownRanks()
    {
        // Arrange - Test formula: RRF(d) = 1 / (k + rank)
        var config = Options.Create(_defaultConfig); // k=60
        var reranker = new RRFReranker(config, _loggerMock.Object);

        var doc1 = Guid.NewGuid();

        // Single result at rank 1
        var results = new List<RetrievalResult>
        {
            new(doc1, 0.9, "text1", "source1")
        };

        var resultSets = new List<List<RetrievalResult>> { results };

        // Act
        var reranked = reranker.Rerank(resultSets, 1);

        // Assert
        // Formula: 1 / (60 + 1) = 1/61 ≈ 0.0163934
        var expectedScore = 1.0 / 61.0;
        reranked[0].Score.ShouldBe(expectedScore, 0.000001);
    }

    #endregion

    #region Sorting and TopK Tests

    [Fact]
    public void Rerank_ReturnsSortedByRRFScoreDescending()
    {
        // Arrange
        var config = Options.Create(_defaultConfig);
        var reranker = new RRFReranker(config, _loggerMock.Object);

        var doc1 = Guid.NewGuid();
        var doc2 = Guid.NewGuid();
        var doc3 = Guid.NewGuid();
        var doc4 = Guid.NewGuid();

        // Create result sets with different rankings
        var set1 = new List<RetrievalResult>
        {
            new(doc1, 0.9, "text1", "source1"), // Rank 1
            new(doc2, 0.8, "text2", "source2"), // Rank 2
            new(doc3, 0.7, "text3", "source3")  // Rank 3
        };

        var set2 = new List<RetrievalResult>
        {
            new(doc4, 0.95, "text4", "source4"), // Rank 1
            new(doc2, 0.85, "text2", "source2"), // Rank 2 (appears in both sets)
            new(doc1, 0.75, "text1", "source1")  // Rank 3 (appears in both sets)
        };

        var resultSets = new List<List<RetrievalResult>> { set1, set2 };

        // Act
        var reranked = reranker.Rerank(resultSets, 10);

        // Assert
        reranked.Count.ShouldBe(4);

        // Verify sorted by RRF score descending
        for (int i = 0; i < reranked.Count - 1; i++)
        {
            reranked[i].Score.ShouldBeGreaterThanOrEqualTo(reranked[i + 1].Score);
        }

        // doc1 and doc2 appear in both sets, should have highest scores
        reranked[0].DocumentId.ShouldBeOneOf(doc1, doc2);
        reranked[1].DocumentId.ShouldBeOneOf(doc1, doc2);
    }

    [Fact]
    public void Rerank_RespectsTopKLimit()
    {
        // Arrange
        var config = Options.Create(_defaultConfig);
        var reranker = new RRFReranker(config, _loggerMock.Object);

        var results = new List<RetrievalResult>();
        for (int i = 0; i < 20; i++)
        {
            results.Add(new RetrievalResult(Guid.NewGuid(), 0.9 - (i * 0.01), $"text{i}", $"source{i}"));
        }

        var resultSets = new List<List<RetrievalResult>> { results };

        // Act
        var reranked = reranker.Rerank(resultSets, 5);

        // Assert
        reranked.Count.ShouldBe(5); // Only top 5 returned
    }

    #endregion

    #region Deduplication Tests

    [Fact]
    public void Rerank_DeduplicatesDocumentsByDocumentId()
    {
        // Arrange
        var config = Options.Create(_defaultConfig);
        var reranker = new RRFReranker(config, _loggerMock.Object);

        var doc1 = Guid.NewGuid();

        // Same document in both result sets (different scores/text)
        var set1 = new List<RetrievalResult>
        {
            new(doc1, 0.9, "text from BM25", "bm25")
        };

        var set2 = new List<RetrievalResult>
        {
            new(doc1, 0.8, "text from Dense", "dense")
        };

        var resultSets = new List<List<RetrievalResult>> { set1, set2 };

        // Act
        var reranked = reranker.Rerank(resultSets, 10);

        // Assert
        reranked.Count.ShouldBe(1); // Only 1 unique document
        reranked[0].DocumentId.ShouldBe(doc1);

        // RRF score should be sum: 1/(60+1) + 1/(60+1) = 2/61 ≈ 0.03279
        reranked[0].Score.ShouldBe(2.0 / 61.0, 0.0001);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Rerank_WithDifferentKValues_ChangesScores()
    {
        // Arrange - Test with K=30 instead of default 60
        var customConfig = new RRFConfig { K = 30 };
        var configOptions = Options.Create(customConfig);
        var reranker = new RRFReranker(configOptions, _loggerMock.Object);

        var doc1 = Guid.NewGuid();

        var results = new List<RetrievalResult>
        {
            new(doc1, 0.9, "text1", "source1") // Rank 1
        };

        var resultSets = new List<List<RetrievalResult>> { results };

        // Act
        var reranked = reranker.Rerank(resultSets, 1);

        // Assert
        // Formula with K=30: 1 / (30 + 1) = 1/31 ≈ 0.03226
        var expectedScore = 1.0 / 31.0;
        reranked[0].Score.ShouldBe(expectedScore, 0.000001);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Rerank_WithEmptyResultSets_ReturnsEmptyList()
    {
        // Arrange
        var config = Options.Create(_defaultConfig);
        var reranker = new RRFReranker(config, _loggerMock.Object);

        var resultSets = new List<List<RetrievalResult>>();

        // Act
        var reranked = reranker.Rerank(resultSets, 10);

        // Assert
        reranked.ShouldBeEmpty();
    }

    [Fact]
    public void Rerank_WithAllEmptyInnerLists_ReturnsEmptyList()
    {
        // Arrange
        var config = Options.Create(_defaultConfig);
        var reranker = new RRFReranker(config, _loggerMock.Object);

        var resultSets = new List<List<RetrievalResult>>
        {
            new List<RetrievalResult>(),
            new List<RetrievalResult>(),
            new List<RetrievalResult>()
        };

        // Act
        var reranked = reranker.Rerank(resultSets, 10);

        // Assert
        reranked.ShouldBeEmpty();
    }

    #endregion

    #region Input Validation Tests

    [Fact]
    public void Rerank_WithNullResultSets_ThrowsArgumentNullException()
    {
        // Arrange
        var config = Options.Create(_defaultConfig);
        var reranker = new RRFReranker(config, _loggerMock.Object);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            reranker.Rerank(null!, 10));
    }

    [Fact]
    public void Rerank_WithZeroTopK_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var config = Options.Create(_defaultConfig);
        var reranker = new RRFReranker(config, _loggerMock.Object);

        var resultSets = new List<List<RetrievalResult>>
        {
            new List<RetrievalResult>
            {
                new(Guid.NewGuid(), 0.9, "text1", "source1")
            }
        };

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() =>
            reranker.Rerank(resultSets, 0));
    }

    [Fact]
    public void Rerank_WithNegativeTopK_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var config = Options.Create(_defaultConfig);
        var reranker = new RRFReranker(config, _loggerMock.Object);

        var resultSets = new List<List<RetrievalResult>>
        {
            new List<RetrievalResult>
            {
                new(Guid.NewGuid(), 0.9, "text1", "source1")
            }
        };

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() =>
            reranker.Rerank(resultSets, -1));
    }

    #endregion
}
