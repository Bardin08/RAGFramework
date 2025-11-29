using Microsoft.Extensions.Logging;
using Moq;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Core.Domain.Enums;
using RAG.Infrastructure.Retrievers;
using Shouldly;

namespace RAG.Tests.Integration.Retrievers;

/// <summary>
/// Integration tests for AdaptiveRetriever.
/// Tests adaptive retrieval behavior with query classification integration.
/// </summary>
public class AdaptiveRetrieverIntegrationTests
{
    private readonly Mock<IQueryClassifier> _queryClassifierMock;
    private readonly Mock<IRetriever> _bm25RetrieverMock;
    private readonly Mock<IRetriever> _denseRetrieverMock;
    private readonly Mock<IRetriever> _hybridRetrieverMock;
    private readonly Mock<ILogger<AdaptiveRetriever>> _loggerMock;

    public AdaptiveRetrieverIntegrationTests()
    {
        _queryClassifierMock = new Mock<IQueryClassifier>();
        _bm25RetrieverMock = new Mock<IRetriever>();
        _denseRetrieverMock = new Mock<IRetriever>();
        _hybridRetrieverMock = new Mock<IRetriever>();
        _loggerMock = new Mock<ILogger<AdaptiveRetriever>>();
    }

    [Fact]
    public async Task SearchAsync_WithExplicitFactQuery_RoutesToBM25()
    {
        // Arrange
        var query = "What is the capital of France?";
        var topK = 10;
        var tenantId = Guid.NewGuid();
        var expectedResults = CreateMockResults(5, "bm25");

        _queryClassifierMock
            .Setup(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryType.ExplicitFact);

        _bm25RetrieverMock
            .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        var adaptiveRetriever = CreateRetriever();

        // Act
        var results = await adaptiveRetriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.ShouldBe(expectedResults);
        _queryClassifierMock.Verify(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
        _bm25RetrieverMock.Verify(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _denseRetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _hybridRetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WithImplicitFactQuery_RoutesToHybrid()
    {
        // Arrange
        var query = "Why is RAG effective for question answering?";
        var topK = 10;
        var tenantId = Guid.NewGuid();
        var expectedResults = CreateMockResults(5, "hybrid");

        _queryClassifierMock
            .Setup(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryType.ImplicitFact);

        _hybridRetrieverMock
            .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        var adaptiveRetriever = CreateRetriever();

        // Act
        var results = await adaptiveRetriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.ShouldBe(expectedResults);
        _queryClassifierMock.Verify(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
        _hybridRetrieverMock.Verify(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _bm25RetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _denseRetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WithInterpretableRationaleQuery_RoutesToDense()
    {
        // Arrange
        var query = "Compare BM25 and dense retrieval approaches";
        var topK = 10;
        var tenantId = Guid.NewGuid();
        var expectedResults = CreateMockResults(5, "dense");

        _queryClassifierMock
            .Setup(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryType.InterpretableRationale);

        _denseRetrieverMock
            .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        var adaptiveRetriever = CreateRetriever();

        // Act
        var results = await adaptiveRetriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.ShouldBe(expectedResults);
        _queryClassifierMock.Verify(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
        _denseRetrieverMock.Verify(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _bm25RetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _hybridRetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WithHiddenRationaleQuery_RoutesToDense()
    {
        // Arrange
        var query = "Should we implement hybrid search for our application?";
        var topK = 10;
        var tenantId = Guid.NewGuid();
        var expectedResults = CreateMockResults(5, "dense");

        _queryClassifierMock
            .Setup(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryType.HiddenRationale);

        _denseRetrieverMock
            .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        var adaptiveRetriever = CreateRetriever();

        // Act
        var results = await adaptiveRetriever.SearchAsync(query, topK, tenantId);

        // Assert
        results.ShouldBe(expectedResults);
        _queryClassifierMock.Verify(x => x.ClassifyQueryAsync(query, It.IsAny<CancellationToken>()), Times.Once);
        _denseRetrieverMock.Verify(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _bm25RetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _hybridRetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WithMultipleQueryTypes_RoutesDynamically()
    {
        // Arrange
        var topK = 10;
        var tenantId = Guid.NewGuid();

        var explicitQuery = "What is BM25?";
        var implicitQuery = "Why is hybrid search better?";
        var rationaleQuery = "Compare different retrieval strategies";

        var bm25Results = CreateMockResults(5, "bm25");
        var hybridResults = CreateMockResults(5, "hybrid");
        var denseResults = CreateMockResults(5, "dense");

        _queryClassifierMock
            .Setup(x => x.ClassifyQueryAsync(explicitQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryType.ExplicitFact);
        _queryClassifierMock
            .Setup(x => x.ClassifyQueryAsync(implicitQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryType.ImplicitFact);
        _queryClassifierMock
            .Setup(x => x.ClassifyQueryAsync(rationaleQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryType.InterpretableRationale);

        _bm25RetrieverMock
            .Setup(x => x.SearchAsync(explicitQuery, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bm25Results);
        _hybridRetrieverMock
            .Setup(x => x.SearchAsync(implicitQuery, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hybridResults);
        _denseRetrieverMock
            .Setup(x => x.SearchAsync(rationaleQuery, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(denseResults);

        var adaptiveRetriever = CreateRetriever();

        // Act
        var explicitResults = await adaptiveRetriever.SearchAsync(explicitQuery, topK, tenantId);
        var implicitResults = await adaptiveRetriever.SearchAsync(implicitQuery, topK, tenantId);
        var rationaleResults = await adaptiveRetriever.SearchAsync(rationaleQuery, topK, tenantId);

        // Assert
        explicitResults.ShouldBe(bm25Results);
        implicitResults.ShouldBe(hybridResults);
        rationaleResults.ShouldBe(denseResults);

        _bm25RetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        _hybridRetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        _denseRetrieverMock.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_WithManualOverride_BypassesClassification()
    {
        // Arrange
        var query = "What is the capital of France?";
        var topK = 10;
        var tenantId = Guid.NewGuid();
        var expectedResults = CreateMockResults(5, "dense");

        _denseRetrieverMock
            .Setup(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        var adaptiveRetriever = CreateRetriever();

        // Act
        var results = await adaptiveRetriever.SearchAsync(query, topK, tenantId, "dense");

        // Assert
        results.ShouldBe(expectedResults);
        _denseRetrieverMock.Verify(x => x.SearchAsync(query, topK, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _queryClassifierMock.Verify(x => x.ClassifyQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private AdaptiveRetriever CreateRetriever()
    {
        return new AdaptiveRetriever(
            _queryClassifierMock.Object,
            _bm25RetrieverMock.Object,
            _denseRetrieverMock.Object,
            _hybridRetrieverMock.Object,
            _loggerMock.Object);
    }

    private static List<RetrievalResult> CreateMockResults(int count, string strategy)
    {
        var results = new List<RetrievalResult>();
        for (int i = 0; i < count; i++)
        {
            results.Add(new RetrievalResult(
                DocumentId: Guid.NewGuid(),
                Score: 0.9 - (i * 0.1),
                Text: $"{strategy} test content {i}",
                Source: $"{strategy}_doc{i}.pdf",
                HighlightedText: null
            ));
        }
        return results;
    }
}
