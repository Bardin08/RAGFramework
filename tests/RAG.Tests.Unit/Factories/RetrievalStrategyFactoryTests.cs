using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RAG.Application.Interfaces;
using RAG.Application.Reranking;
using RAG.Core.Configuration;
using RAG.Core.Enums;
using RAG.Infrastructure.Factories;
using RAG.Infrastructure.Retrievers;
using Shouldly;

namespace RAG.Tests.Unit.Factories;

public class RetrievalStrategyFactoryTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ILogger<RetrievalStrategyFactory>> _mockLogger;
    private readonly RetrievalStrategyFactory _factory;

    public RetrievalStrategyFactoryTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockLogger = new Mock<ILogger<RetrievalStrategyFactory>>();
        _factory = new RetrievalStrategyFactory(_mockServiceProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public void CreateStrategy_BM25_ReturnsBM25Retriever()
    {
        // Arrange
        var bm25Settings = Options.Create(new BM25Settings
        {
            K1 = 1.2,
            B = 0.75,
            DefaultTopK = 10,
            MaxTopK = 100,
            TimeoutSeconds = 30,
            HighlightFragmentSize = 150
        });

        var elasticsearchSettings = Options.Create(new ElasticsearchSettings
        {
            Url = "http://localhost:9200",
            IndexName = "test-index",
            Username = "test",
            Password = "test"
        });

        var mockBM25Retriever = new Mock<BM25Retriever>(
            bm25Settings,
            elasticsearchSettings,
            Mock.Of<ILogger<BM25Retriever>>());

        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(BM25Retriever)))
            .Returns(mockBM25Retriever.Object);

        // Act
        var strategy = _factory.CreateStrategy(RetrievalStrategyType.BM25);

        // Assert
        strategy.ShouldNotBeNull();
        strategy.ShouldBeAssignableTo<IRetrievalStrategy>();
        strategy.GetStrategyName().ShouldBe("BM25");
        strategy.StrategyType.ShouldBe(RetrievalStrategyType.BM25);
    }

    [Fact]
    public void CreateStrategy_Dense_ReturnsDenseRetriever()
    {
        // Arrange
        var denseSettings = Options.Create(new DenseSettings
        {
            DefaultTopK = 10,
            MaxTopK = 100,
            SimilarityThreshold = 0.7,
            EmbeddingTimeoutSeconds = 30,
            QdrantTimeoutSeconds = 30,
            TimeoutSeconds = 60
        });

        var mockDenseRetriever = new Mock<DenseRetriever>(
            denseSettings,
            Mock.Of<IEmbeddingService>(),
            Mock.Of<IVectorStoreClient>(),
            Mock.Of<ILogger<DenseRetriever>>());

        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(DenseRetriever)))
            .Returns(mockDenseRetriever.Object);

        // Act
        var strategy = _factory.CreateStrategy(RetrievalStrategyType.Dense);

        // Assert
        strategy.ShouldNotBeNull();
        strategy.ShouldBeAssignableTo<IRetrievalStrategy>();
        strategy.GetStrategyName().ShouldBe("Dense");
        strategy.StrategyType.ShouldBe(RetrievalStrategyType.Dense);
    }

    [Fact]
    public void CreateStrategy_Hybrid_ReturnsHybridRetriever()
    {
        // Arrange
        var hybridConfig = Options.Create(new HybridSearchConfig
        {
            Alpha = 0.5,
            Beta = 0.5,
            IntermediateK = 20
        });

        var mockHybridRetriever = new Mock<HybridRetriever>(
            Mock.Of<IRetriever>(),
            Mock.Of<IRetriever>(),
            Mock.Of<IRRFReranker>(),
            hybridConfig,
            Mock.Of<ILogger<HybridRetriever>>());

        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(HybridRetriever)))
            .Returns(mockHybridRetriever.Object);

        // Act
        var strategy = _factory.CreateStrategy(RetrievalStrategyType.Hybrid);

        // Assert
        strategy.ShouldNotBeNull();
        strategy.ShouldBeAssignableTo<IRetrievalStrategy>();
        strategy.GetStrategyName().ShouldBe("Hybrid");
        strategy.StrategyType.ShouldBe(RetrievalStrategyType.Hybrid);
    }

    [Fact]
    public void CreateStrategy_InvalidType_ThrowsArgumentException()
    {
        // Arrange
        var invalidType = (RetrievalStrategyType)999;

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() =>
            _factory.CreateStrategy(invalidType));

        exception.Message.ShouldContain("Unknown retrieval strategy type");
        exception.ParamName.ShouldBe("type");
    }

    [Fact]
    public void CreateStrategy_BM25_LogsStrategyCreation()
    {
        // Arrange
        var bm25Settings = Options.Create(new BM25Settings
        {
            K1 = 1.2,
            B = 0.75,
            DefaultTopK = 10,
            MaxTopK = 100,
            TimeoutSeconds = 30,
            HighlightFragmentSize = 150
        });

        var elasticsearchSettings = Options.Create(new ElasticsearchSettings
        {
            Url = "http://localhost:9200",
            IndexName = "test-index",
            Username = "test",
            Password = "test"
        });

        var mockBM25Retriever = new Mock<BM25Retriever>(
            bm25Settings,
            elasticsearchSettings,
            Mock.Of<ILogger<BM25Retriever>>());

        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(BM25Retriever)))
            .Returns(mockBM25Retriever.Object);

        // Act
        _factory.CreateStrategy(RetrievalStrategyType.BM25);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Creating retrieval strategy")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created retrieval strategy")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_NullServiceProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Should.Throw<ArgumentNullException>(() =>
            new RetrievalStrategyFactory(null!, _mockLogger.Object));

        exception.ParamName.ShouldBe("serviceProvider");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Should.Throw<ArgumentNullException>(() =>
            new RetrievalStrategyFactory(_mockServiceProvider.Object, null!));

        exception.ParamName.ShouldBe("logger");
    }
}
