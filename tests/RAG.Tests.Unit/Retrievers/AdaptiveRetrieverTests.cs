using Microsoft.Extensions.Logging;
using Moq;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Core.Domain.Enums;
using RAG.Core.Enums;
using RAG.Infrastructure.Retrievers;
using Shouldly;

namespace RAG.Tests.Unit.Retrievers;

/// <summary>
/// Unit tests for AdaptiveRetriever class.
/// Tests query-aware strategy selection, manual override, logging, and error handling.
/// </summary>
public class AdaptiveRetrieverTests
{
    private readonly Mock<IQueryClassifier> _queryClassifierMock;
    private readonly Mock<IRetriever> _bm25RetrieverMock;
    private readonly Mock<IRetriever> _denseRetrieverMock;
    private readonly Mock<IRetriever> _hybridRetrieverMock;
    private readonly Mock<ILogger<AdaptiveRetriever>> _loggerMock;

    public AdaptiveRetrieverTests()
    {
        _queryClassifierMock = new Mock<IQueryClassifier>();
        _bm25RetrieverMock = new Mock<IRetriever>();
        _denseRetrieverMock = new Mock<IRetriever>();
        _hybridRetrieverMock = new Mock<IRetriever>();
        _loggerMock = new Mock<ILogger<AdaptiveRetriever>>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange & Act
        var retriever = new AdaptiveRetriever(
            _queryClassifierMock.Object,
            _bm25RetrieverMock.Object,
            _denseRetrieverMock.Object,
            _hybridRetrieverMock.Object,
            _loggerMock.Object);

        // Assert
        retriever.ShouldNotBeNull();
        retriever.GetStrategyName().ShouldBe("Adaptive");
        retriever.StrategyType.ShouldBe(RetrievalStrategyType.Adaptive);
    }

    [Fact]
    public void Constructor_WithNullQueryClassifier_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new AdaptiveRetriever(
                null!,
                _bm25RetrieverMock.Object,
                _denseRetrieverMock.Object,
                _hybridRetrieverMock.Object,
                _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullBM25Retriever_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new AdaptiveRetriever(
                _queryClassifierMock.Object,
                null!,
                _denseRetrieverMock.Object,
                _hybridRetrieverMock.Object,
                _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullDenseRetriever_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new AdaptiveRetriever(
                _queryClassifierMock.Object,
                _bm25RetrieverMock.Object,
                null!,
                _hybridRetrieverMock.Object,
                _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullHybridRetriever_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new AdaptiveRetriever(
                _queryClassifierMock.Object,
                _bm25RetrieverMock.Object,
                _denseRetrieverMock.Object,
                null!,
                _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new AdaptiveRetriever(
                _queryClassifierMock.Object,
                _bm25RetrieverMock.Object,
                _denseRetrieverMock.Object,
                _hybridRetrieverMock.Object,
                null!));
    }

    #endregion

    #region Strategy Identification Tests

    [Fact]
    public void GetStrategyName_ReturnsAdaptive()
    {
        // Arrange
        var retriever = CreateRetriever();

        // Act
        var name = retriever.GetStrategyName();

        // Assert
        name.ShouldBe("Adaptive");
    }

    [Fact]
    public void StrategyType_ReturnsAdaptive()
    {
        // Arrange
        var retriever = CreateRetriever();

        // Act
        var type = retriever.StrategyType;

        // Assert
        type.ShouldBe(RetrievalStrategyType.Adaptive);
    }

    #endregion

    #region Query Type Routing Tests

    [Fact]
    public async Task SearchAsync_WithExplicitFactQuery_RoutesToBM25Retriever()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "What is machine learning?";
        var topK = 10;
        var tenantId = Guid.NewGuid();
        var expectedResults = CreateMockResults(5);

        _queryClassifierMock
            .Setup(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryType.ExplicitFact);

        _bm25RetrieverMock
            .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.ShouldBe(expectedResults);
        _queryClassifierMock.Verify(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
        _bm25RetrieverMock.Verify(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _denseRetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _hybridRetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WithImplicitFactQuery_RoutesToHybridRetriever()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "Why is RAG effective?";
        var topK = 10;
        var tenantId = Guid.NewGuid();
        var expectedResults = CreateMockResults(5);

        _queryClassifierMock
            .Setup(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryType.ImplicitFact);

        _hybridRetrieverMock
            .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.ShouldBe(expectedResults);
        _queryClassifierMock.Verify(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
        _hybridRetrieverMock.Verify(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _bm25RetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _denseRetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WithInterpretableRationaleQuery_RoutesToDenseRetriever()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "Compare BM25 and Dense retrieval";
        var topK = 10;
        var tenantId = Guid.NewGuid();
        var expectedResults = CreateMockResults(5);

        _queryClassifierMock
            .Setup(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryType.InterpretableRationale);

        _denseRetrieverMock
            .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.ShouldBe(expectedResults);
        _queryClassifierMock.Verify(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
        _denseRetrieverMock.Verify(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _bm25RetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _hybridRetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

    }

    [Fact]
    public async Task SearchAsync_WithHiddenRationaleQuery_RoutesToDenseRetriever()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "Should we use hybrid search?";
        var topK = 10;
        var tenantId = Guid.NewGuid();
        var expectedResults = CreateMockResults(5);

        _queryClassifierMock
            .Setup(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryType.HiddenRationale);

        _denseRetrieverMock
            .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.ShouldBe(expectedResults);
        _queryClassifierMock.Verify(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
        _denseRetrieverMock.Verify(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _bm25RetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _hybridRetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

    }

    #endregion

    #region Manual Override Tests

    [Fact]
    public async Task SearchAsync_WithManualOverrideBM25_RoutesToBM25RegardlessOfClassification()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "Why is RAG effective?"; // Would normally route to Hybrid
        var topK = 10;
        var tenantId = Guid.NewGuid();
        var expectedResults = CreateMockResults(5);

        _bm25RetrieverMock
            .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId, "bm25");

        // Assert
        results.ShouldBe(expectedResults);
        _queryClassifierMock.Verify(x => x.ClassifyQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _bm25RetrieverMock.Verify(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _denseRetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _hybridRetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

    }

    [Fact]
    public async Task SearchAsync_WithManualOverrideDense_RoutesToDenseRegardlessOfClassification()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "What is machine learning?"; // Would normally route to BM25
        var topK = 10;
        var tenantId = Guid.NewGuid();
        var expectedResults = CreateMockResults(5);

        _denseRetrieverMock
            .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId, "dense");

        // Assert
        results.ShouldBe(expectedResults);
        _queryClassifierMock.Verify(x => x.ClassifyQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _denseRetrieverMock.Verify(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _bm25RetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _hybridRetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

    }

    [Fact]
    public async Task SearchAsync_WithManualOverrideHybrid_RoutesToHybridRegardlessOfClassification()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "What is machine learning?"; // Would normally route to BM25
        var topK = 10;
        var tenantId = Guid.NewGuid();
        var expectedResults = CreateMockResults(5);

        _hybridRetrieverMock
            .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId, "hybrid");

        // Assert
        results.ShouldBe(expectedResults);
        _queryClassifierMock.Verify(x => x.ClassifyQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _hybridRetrieverMock.Verify(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _bm25RetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _denseRetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

    }

    [Theory]
    [InlineData("BM25")]
    [InlineData("bm25")]
    [InlineData("Bm25")]
    [InlineData("Dense")]
    [InlineData("DENSE")]
    [InlineData("Hybrid")]
    [InlineData("HYBRID")]
    public async Task SearchAsync_WithCaseInsensitiveOverride_RoutesCorrectly(string strategyOverride)
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "Test query";
        var topK = 10;
        var tenantId = Guid.NewGuid();
        var expectedResults = CreateMockResults(5);

        var strategyLower = strategyOverride.ToLower();

        if (strategyLower == "bm25")
        {
            _bm25RetrieverMock
                .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResults);
        }
        else if (strategyLower == "dense")
        {
            _denseRetrieverMock
                .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResults);
        }
        else if (strategyLower == "hybrid")
        {
            _hybridRetrieverMock
                .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResults);
        }

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId, strategyOverride);

        // Assert
        results.ShouldBe(expectedResults);
        _queryClassifierMock.Verify(x => x.ClassifyQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void SearchAsync_WithInvalidOverride_ThrowsArgumentException()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "Test query";
        var topK = 10;
        var tenantId = Guid.NewGuid();

        // Act & Assert
        Should.Throw<ArgumentException>(async () =>
            await retriever.SearchAsync(query, topK, tenantId, "invalid"));
    }

    [Fact]
    public async Task SearchAsync_WithNullOverride_UsesClassificationPath()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "What is machine learning?";
        var topK = 10;
        var tenantId = Guid.NewGuid();
        var expectedResults = CreateMockResults(5);

        _queryClassifierMock
            .Setup(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryType.ExplicitFact);

        _bm25RetrieverMock
            .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId, null);

        // Assert
        results.ShouldBe(expectedResults);
        _queryClassifierMock.Verify(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
        _bm25RetrieverMock.Verify(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_WithEmptyOverride_UsesClassificationPath()
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "What is machine learning?";
        var topK = 10;
        var tenantId = Guid.NewGuid();
        var expectedResults = CreateMockResults(5);

        _queryClassifierMock
            .Setup(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryType.ExplicitFact);

        _bm25RetrieverMock
            .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await retriever.SearchAsync(query, topK, tenantId, "");

        // Assert
        results.ShouldBe(expectedResults);
        _queryClassifierMock.Verify(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
        _bm25RetrieverMock.Verify(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Input Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_WithNullOrEmptyQuery_ThrowsArgumentException(string? query)
    {
        // Arrange
        var retriever = CreateRetriever();
        var topK = 10;
        var tenantId = Guid.NewGuid();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await retriever.SearchAsync(query!, topK, tenantId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task SearchAsync_WithNonPositiveTopK_ThrowsArgumentOutOfRangeException(int topK)
    {
        // Arrange
        var retriever = CreateRetriever();
        var query = "Test query";
        var tenantId = Guid.NewGuid();

        // Act & Assert
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await retriever.SearchAsync(query, topK, tenantId));
    }

    #endregion

    #region Helper Methods

    private AdaptiveRetriever CreateRetriever()
    {
        return new AdaptiveRetriever(
            _queryClassifierMock.Object,
            _bm25RetrieverMock.Object,
            _denseRetrieverMock.Object,
            _hybridRetrieverMock.Object,
            _loggerMock.Object);
    }

    private static List<RetrievalResult> CreateMockResults(int count)
    {
        var results = new List<RetrievalResult>();
        for (int i = 0; i < count; i++)
        {
            results.Add(new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.9 - (i * 0.1),
                Text: $"Test content {i}",
                Source: $"doc{i}.pdf",
                HighlightedText: null
            ));
        }
        return results;
    }

    #endregion
}
