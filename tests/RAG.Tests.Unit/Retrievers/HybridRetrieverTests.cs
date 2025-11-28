using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;
using RAG.Core.Domain;
using RAG.Infrastructure.Retrievers;
using Shouldly;

namespace RAG.Tests.Unit.Retrievers;

public class HybridRetrieverTests
{
    private readonly Mock<IRetriever> _bm25RetrieverMock;
    private readonly Mock<IRetriever> _denseRetrieverMock;
    private readonly Mock<ILogger<HybridRetriever>> _loggerMock;
    private readonly HybridSearchConfig _config;
    private readonly FakeBM25Retriever _fakeBM25Retriever;
    private readonly FakeDenseRetriever _fakeDenseRetriever;

    public HybridRetrieverTests()
    {
        _bm25RetrieverMock = new Mock<IRetriever>();
        _denseRetrieverMock = new Mock<IRetriever>();
        _loggerMock = new Mock<ILogger<HybridRetriever>>();

        _config = new HybridSearchConfig
        {
            Alpha = 0.5,
            Beta = 0.5,
            IntermediateK = 20
        };

        // Create fake retrievers for tests that need concrete implementations
        _fakeBM25Retriever = new FakeBM25Retriever();
        _fakeDenseRetriever = new FakeDenseRetriever();
    }

    // Fake implementations for testing
    private class FakeBM25Retriever : IRetriever
    {
        public List<RetrievalResult>? ResultsToReturn { get; set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task<List<RetrievalResult>> SearchAsync(string query, int topK, Guid tenantId, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null)
                throw ExceptionToThrow;

            return Task.FromResult(ResultsToReturn ?? new List<RetrievalResult>());
        }
    }

    private class FakeDenseRetriever : IRetriever
    {
        public List<RetrievalResult>? ResultsToReturn { get; set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task<List<RetrievalResult>> SearchAsync(string query, int topK, Guid tenantId, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null)
                throw ExceptionToThrow;

            return Task.FromResult(ResultsToReturn ?? new List<RetrievalResult>());
        }
    }

    [Fact]
    public void Constructor_WithNullBM25Retriever_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new HybridRetriever(null!, _fakeDenseRetriever, Options.Create(_config), _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullDenseRetriever_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new HybridRetriever(_fakeBM25Retriever, null!, Options.Create(_config), _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new HybridRetriever(_fakeBM25Retriever, _fakeDenseRetriever, null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new HybridRetriever(_fakeBM25Retriever, _fakeDenseRetriever, Options.Create(_config), null!));
    }

    [Fact]
    public void Constructor_WithInvalidConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidConfig = new HybridSearchConfig
        {
            Alpha = 0.6,
            Beta = 0.6, // Sum = 1.2, invalid
            IntermediateK = 20
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            new HybridRetriever(_fakeBM25Retriever, _fakeDenseRetriever, Options.Create(invalidConfig), _loggerMock.Object));
    }

    [Fact]
    public void GetStrategyName_ReturnsHybrid()
    {
        // Arrange
        var retriever = new HybridRetriever(_fakeBM25Retriever, _fakeDenseRetriever, Options.Create(_config), _loggerMock.Object);

        // Act
        var strategyName = retriever.GetStrategyName();

        // Assert
        strategyName.ShouldBe("Hybrid");
    }

    [Fact]
    public void StrategyType_ReturnsHybrid()
    {
        // Arrange
        var retriever = new HybridRetriever(_fakeBM25Retriever, _fakeDenseRetriever, Options.Create(_config), _loggerMock.Object);

        // Act
        var strategyType = retriever.StrategyType;

        // Assert
        strategyType.ShouldBe(RAG.Core.Enums.RetrievalStrategyType.Hybrid);
    }

    [Fact]
    public async Task SearchAsync_WithValidQuery_CallsBothRetrieversInParallel()
    {
        // Arrange
        var query = "test query";
        var topK = 5;
        var tenantId = Guid.NewGuid();

        var bm25Results = CreateMockResults(10, 0.9, 0.1);
        var denseResults = CreateMockResults(10, 0.95, 0.15);

        _fakeBM25Retriever.ResultsToReturn = bm25Results;
        _fakeDenseRetriever.ResultsToReturn = denseResults;

        var retriever = new HybridRetriever(_fakeBM25Retriever, _fakeDenseRetriever, Options.Create(_config), _loggerMock.Object);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.ShouldNotBeNull();
        results.Count.ShouldBeLessThanOrEqualTo(topK);
        results.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SearchAsync_WithAlpha05Beta05_CalculatesCorrectWeightedScores()
    {
        // Arrange
        var query = "test query";
        var topK = 10;
        var tenantId = Guid.NewGuid();

        // BM25 returns results with scores 0.9, 0.8, 0.7 (will be normalized to 1.0, 0.5, 0.0)
        var doc1 = Guid.NewGuid();
        var doc2 = Guid.NewGuid();
        var doc3 = Guid.NewGuid();

        var bm25Results = new List<RetrievalResult>
        {
            new(doc1, 0.9, "text1", "source1"),
            new(doc2, 0.8, "text2", "source2"),
            new(doc3, 0.7, "text3", "source3")
        };

        // Dense returns results with scores already normalized: 0.95, 0.85, 0.75
        var denseResults = new List<RetrievalResult>
        {
            new(doc1, 0.95, "text1", "source1"),
            new(doc2, 0.85, "text2", "source2"),
            new(doc3, 0.75, "text3", "source3")
        };

        _fakeBM25Retriever.ResultsToReturn = bm25Results;
        _fakeDenseRetriever.ResultsToReturn = denseResults;

        var retriever = new HybridRetriever(_fakeBM25Retriever, _fakeDenseRetriever, Options.Create(_config), _loggerMock.Object);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.ShouldNotBeNull();
        results.Count.ShouldBe(3);

        // Expected scores after normalization and weighting (alpha=0.5, beta=0.5):
        // doc1: 0.5 * 1.0 (normalized BM25) + 0.5 * 0.95 (dense) = 0.5 + 0.475 = 0.975
        // doc2: 0.5 * 0.5 (normalized BM25) + 0.5 * 0.85 (dense) = 0.25 + 0.425 = 0.675
        // doc3: 0.5 * 0.0 (normalized BM25) + 0.5 * 0.75 (dense) = 0.0 + 0.375 = 0.375

        results[0].DocumentId.ShouldBe(doc1);
        results[0].Score.ShouldBe(0.975, 0.001);

        results[1].DocumentId.ShouldBe(doc2);
        results[1].Score.ShouldBe(0.675, 0.001);

        results[2].DocumentId.ShouldBe(doc3);
        results[2].Score.ShouldBe(0.375, 0.001);
    }

    [Fact]
    public async Task SearchAsync_WithTopK5_ReturnsSortedTop5Results()
    {
        // Arrange
        var query = "test query";
        var topK = 5;
        var tenantId = Guid.NewGuid();

        var bm25Results = CreateMockResults(20, 0.9, 0.1);
        var denseResults = CreateMockResults(20, 0.95, 0.15);

        _fakeBM25Retriever.ResultsToReturn = bm25Results;
        _fakeDenseRetriever.ResultsToReturn = denseResults;

        var retriever = new HybridRetriever(_fakeBM25Retriever, _fakeDenseRetriever, Options.Create(_config), _loggerMock.Object);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.ShouldNotBeNull();
        results.Count.ShouldBe(topK);

        // Verify results are sorted by score descending
        for (int i = 0; i < results.Count - 1; i++)
        {
            results[i].Score.ShouldBeGreaterThanOrEqualTo(results[i + 1].Score);
        }
    }

    [Fact]
    public async Task SearchAsync_WithDuplicateDocuments_DeduplicatesCorrectly()
    {
        // Arrange
        var query = "test query";
        var topK = 10;
        var tenantId = Guid.NewGuid();

        var doc1 = Guid.NewGuid();
        var doc2 = Guid.NewGuid();

        // BM25 and Dense return same documents with different scores
        var bm25Results = new List<RetrievalResult>
        {
            new(doc1, 0.9, "text1 from BM25", "source1"),
            new(doc2, 0.8, "text2 from BM25", "source2")
        };

        var denseResults = new List<RetrievalResult>
        {
            new(doc1, 0.95, "text1 from Dense", "source1"),
            new(doc2, 0.85, "text2 from Dense", "source2")
        };

        _fakeBM25Retriever.ResultsToReturn = bm25Results;
        _fakeDenseRetriever.ResultsToReturn = denseResults;

        var retriever = new HybridRetriever(_fakeBM25Retriever, _fakeDenseRetriever, Options.Create(_config), _loggerMock.Object);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.ShouldNotBeNull();
        results.Count.ShouldBe(2); // Only 2 unique documents

        // Verify no duplicate DocumentIds
        results.Select(r => r.DocumentId).Distinct().Count().ShouldBe(2);

        // Combined scores should be alpha * bm25_normalized + beta * dense
        // doc1: 0.5 * 1.0 + 0.5 * 0.95 = 0.975
        // doc2: 0.5 * 0.0 + 0.5 * 0.85 = 0.425
        results[0].DocumentId.ShouldBe(doc1);
        results[0].Score.ShouldBe(0.975, 0.001);
    }

    [Fact]
    public async Task SearchAsync_WhenBM25Fails_ContinuesWithDenseResults()
    {
        // Arrange
        var query = "test query";
        var topK = 5;
        var tenantId = Guid.NewGuid();

        var denseResults = CreateMockResults(10, 0.95, 0.15);

        _fakeBM25Retriever.ExceptionToThrow = new Exception("BM25 retrieval failed");
        _fakeDenseRetriever.ResultsToReturn = denseResults;

        var retriever = new HybridRetriever(_fakeBM25Retriever, _fakeDenseRetriever, Options.Create(_config), _loggerMock.Object);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.ShouldNotBeNull();
        results.Count.ShouldBeLessThanOrEqualTo(topK);
        results.Count.ShouldBeGreaterThan(0); // Should have Dense results
    }

    [Fact]
    public async Task SearchAsync_WhenDenseFails_ContinuesWithBM25Results()
    {
        // Arrange
        var query = "test query";
        var topK = 5;
        var tenantId = Guid.NewGuid();

        var bm25Results = CreateMockResults(10, 0.9, 0.1);

        _fakeBM25Retriever.ResultsToReturn = bm25Results;
        _fakeDenseRetriever.ExceptionToThrow = new Exception("Dense retrieval failed");

        var retriever = new HybridRetriever(_fakeBM25Retriever, _fakeDenseRetriever, Options.Create(_config), _loggerMock.Object);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.ShouldNotBeNull();
        results.Count.ShouldBeLessThanOrEqualTo(topK);
        results.Count.ShouldBeGreaterThan(0); // Should have BM25 results
    }

    [Fact]
    public async Task SearchAsync_WithNullOrEmptyQuery_ThrowsArgumentException()
    {
        // Arrange
        var topK = 5;
        var tenantId = Guid.NewGuid();
        var retriever = new HybridRetriever(_fakeBM25Retriever, _fakeDenseRetriever, Options.Create(_config), _loggerMock.Object);

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await retriever.SearchAsync("", topK, tenantId));

        await Should.ThrowAsync<ArgumentException>(async () =>
            await retriever.SearchAsync(null!, topK, tenantId));
    }

    [Fact]
    public async Task SearchAsync_WithInvalidTopK_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var query = "test query";
        var tenantId = Guid.NewGuid();
        var retriever = new HybridRetriever(_fakeBM25Retriever, _fakeDenseRetriever, Options.Create(_config), _loggerMock.Object);

        // Act & Assert
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await retriever.SearchAsync(query, 0, tenantId));

        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await retriever.SearchAsync(query, -1, tenantId));
    }

    // Helper method to create mock results with scores in descending order
    private static List<RetrievalResult> CreateMockResults(int count, double maxScore, double minScore)
    {
        var results = new List<RetrievalResult>();
        var scoreStep = (maxScore - minScore) / (count - 1);

        for (int i = 0; i < count; i++)
        {
            var score = maxScore - (i * scoreStep);
            results.Add(new RetrievalResult(
                Guid.NewGuid(),
                score,
                $"text{i}",
                $"source{i}"));
        }

        return results;
    }
}
