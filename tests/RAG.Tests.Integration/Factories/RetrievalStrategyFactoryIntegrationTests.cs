using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;
using RAG.Core.Enums;
using RAG.Infrastructure.Factories;
using RAG.Infrastructure.Retrievers;
using Shouldly;

namespace RAG.Tests.Integration.Factories;

/// <summary>
/// Integration tests for RetrievalStrategyFactory with real DI container.
/// Validates factory pattern works correctly with actual service resolution.
/// </summary>
public class RetrievalStrategyFactoryIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public RetrievalStrategyFactoryIntegrationTests()
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // BM25 Settings
                ["BM25Settings:K1"] = "1.2",
                ["BM25Settings:B"] = "0.75",
                ["BM25Settings:DefaultTopK"] = "10",
                ["BM25Settings:MaxTopK"] = "100",
                ["BM25Settings:TimeoutSeconds"] = "30",
                ["BM25Settings:HighlightFragmentSize"] = "150",

                // Dense Settings
                ["DenseSettings:DefaultTopK"] = "10",
                ["DenseSettings:MaxTopK"] = "100",
                ["DenseSettings:SimilarityThreshold"] = "0.7",
                ["DenseSettings:EmbeddingTimeoutSeconds"] = "30",
                ["DenseSettings:QdrantTimeoutSeconds"] = "30",
                ["DenseSettings:TimeoutSeconds"] = "60",

                // Elasticsearch Settings
                ["ElasticsearchSettings:Url"] = "http://localhost:9200",
                ["ElasticsearchSettings:IndexName"] = "test-documents",
                ["ElasticsearchSettings:Username"] = "test",
                ["ElasticsearchSettings:Password"] = "test",

                // Qdrant Settings
                ["QdrantSettings:Url"] = "http://localhost:6333",
                ["QdrantSettings:CollectionName"] = "test-collection",
                ["QdrantSettings:VectorSize"] = "384",
                ["QdrantSettings:Distance"] = "Cosine",

                // Retrieval Settings
                ["RetrievalSettings:DefaultStrategy"] = "Dense",
                ["RetrievalSettings:EnableStrategyFallback"] = "true",
                ["RetrievalSettings:FallbackStrategy"] = "BM25",

                // Hybrid Search Settings
                ["HybridSearch:Alpha"] = "0.5",
                ["HybridSearch:Beta"] = "0.5",
                ["HybridSearch:IntermediateK"] = "20"
            })
            .Build();

        // Build service collection
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole());

        // Register configuration options
        services.Configure<BM25Settings>(configuration.GetSection("BM25Settings"));
        services.Configure<DenseSettings>(configuration.GetSection("DenseSettings"));
        services.Configure<ElasticsearchSettings>(configuration.GetSection("ElasticsearchSettings"));
        services.Configure<QdrantSettings>(configuration.GetSection("QdrantSettings"));
        services.Configure<RetrievalSettings>(configuration.GetSection("RetrievalSettings"));
        services.Configure<HybridSearchConfig>(configuration.GetSection("HybridSearch"));

        // Register mocked dependencies for retrievers
        var mockEmbeddingService = new Mock<IEmbeddingService>();
        services.AddScoped<IEmbeddingService>(_ => mockEmbeddingService.Object);

        var mockVectorStoreClient = new Mock<IVectorStoreClient>();
        services.AddScoped<IVectorStoreClient>(_ => mockVectorStoreClient.Object);

        // Register retrievers as concrete classes (required for factory pattern)
        services.AddScoped<BM25Retriever>();
        services.AddScoped<DenseRetriever>();
        services.AddScoped<HybridRetriever>(sp =>
        {
            var bm25 = sp.GetRequiredService<BM25Retriever>();
            var dense = sp.GetRequiredService<DenseRetriever>();
            var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HybridSearchConfig>>();
            var logger = sp.GetRequiredService<ILogger<HybridRetriever>>();
            return new HybridRetriever(bm25, dense, config, logger);
        });

        // Register factory
        services.AddScoped<RetrievalStrategyFactory>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void Factory_WithRealDI_CreatesBM25Retriever()
    {
        // Arrange
        var factory = _serviceProvider.GetRequiredService<RetrievalStrategyFactory>();

        // Act
        var strategy = factory.CreateStrategy(RetrievalStrategyType.BM25);

        // Assert
        strategy.ShouldNotBeNull();
        strategy.ShouldBeOfType<BM25Retriever>();
        strategy.ShouldBeAssignableTo<IRetrievalStrategy>();
        strategy.GetStrategyName().ShouldBe("BM25");
        strategy.StrategyType.ShouldBe(RetrievalStrategyType.BM25);
    }

    [Fact]
    public void Factory_WithRealDI_CreatesDenseRetriever()
    {
        // Arrange
        var factory = _serviceProvider.GetRequiredService<RetrievalStrategyFactory>();

        // Act
        var strategy = factory.CreateStrategy(RetrievalStrategyType.Dense);

        // Assert
        strategy.ShouldNotBeNull();
        strategy.ShouldBeOfType<DenseRetriever>();
        strategy.ShouldBeAssignableTo<IRetrievalStrategy>();
        strategy.GetStrategyName().ShouldBe("Dense");
        strategy.StrategyType.ShouldBe(RetrievalStrategyType.Dense);
    }

    [Fact]
    public void Factory_WithRealDI_CreatesHybridRetriever()
    {
        // Arrange
        var factory = _serviceProvider.GetRequiredService<RetrievalStrategyFactory>();

        // Act
        var strategy = factory.CreateStrategy(RetrievalStrategyType.Hybrid);

        // Assert
        strategy.ShouldNotBeNull();
        strategy.ShouldBeOfType<HybridRetriever>();
        strategy.ShouldBeAssignableTo<IRetrievalStrategy>();
        strategy.GetStrategyName().ShouldBe("Hybrid");
        strategy.StrategyType.ShouldBe(RetrievalStrategyType.Hybrid);
    }

    [Fact]
    public void Factory_WithRealDI_ScopedLifetime_SameInstanceWithinScope()
    {
        // Arrange
        var factory = _serviceProvider.GetRequiredService<RetrievalStrategyFactory>();

        // Act
        var bm25Strategy1 = factory.CreateStrategy(RetrievalStrategyType.BM25);
        var bm25Strategy2 = factory.CreateStrategy(RetrievalStrategyType.BM25);

        // Assert - Within same scope, DI returns same instance
        bm25Strategy1.ShouldNotBeNull();
        bm25Strategy2.ShouldNotBeNull();
        bm25Strategy1.ShouldBeSameAs(bm25Strategy2);
    }

    [Fact]
    public void Factory_WithRealDI_ScopedLifetime_DifferentInstancesAcrossScopes()
    {
        // Act
        IRetrievalStrategy bm25Strategy1;
        IRetrievalStrategy bm25Strategy2;

        using (var scope1 = _serviceProvider.CreateScope())
        {
            var factory1 = scope1.ServiceProvider.GetRequiredService<RetrievalStrategyFactory>();
            bm25Strategy1 = factory1.CreateStrategy(RetrievalStrategyType.BM25);
        }

        using (var scope2 = _serviceProvider.CreateScope())
        {
            var factory2 = scope2.ServiceProvider.GetRequiredService<RetrievalStrategyFactory>();
            bm25Strategy2 = factory2.CreateStrategy(RetrievalStrategyType.BM25);
        }

        // Assert - Across different scopes, DI returns different instances
        bm25Strategy1.ShouldNotBeNull();
        bm25Strategy2.ShouldNotBeNull();
        bm25Strategy1.ShouldNotBeSameAs(bm25Strategy2);
    }

    [Fact]
    public void Factory_WithRealDI_CanCreateDifferentStrategies()
    {
        // Arrange
        var factory = _serviceProvider.GetRequiredService<RetrievalStrategyFactory>();

        // Act
        var bm25Strategy = factory.CreateStrategy(RetrievalStrategyType.BM25);
        var denseStrategy = factory.CreateStrategy(RetrievalStrategyType.Dense);

        // Assert
        bm25Strategy.ShouldNotBeNull();
        denseStrategy.ShouldNotBeNull();
        bm25Strategy.ShouldNotBeSameAs(denseStrategy);
    }

    [Fact]
    public void Factory_WithRealDI_RetrieversDependenciesResolved()
    {
        // Arrange
        var factory = _serviceProvider.GetRequiredService<RetrievalStrategyFactory>();

        // Act - Create strategies which requires all dependencies to be resolved
        var bm25Strategy = factory.CreateStrategy(RetrievalStrategyType.BM25);
        var denseStrategy = factory.CreateStrategy(RetrievalStrategyType.Dense);

        // Assert - If we get here, DI successfully resolved all dependencies
        bm25Strategy.ShouldNotBeNull();
        denseStrategy.ShouldNotBeNull();
    }

    [Fact]
    public void RetrievalSettings_LoadedFromConfiguration()
    {
        // Arrange
        var retrievalSettings = _serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<RetrievalSettings>>()
            .Value;

        // Act & Assert
        retrievalSettings.DefaultStrategy.ShouldBe("Dense");
        retrievalSettings.EnableStrategyFallback.ShouldBeTrue();
        retrievalSettings.FallbackStrategy.ShouldBe("BM25");
    }

    [Fact]
    public void RetrievalSettings_ValidatesSuccessfully()
    {
        // Arrange
        var retrievalSettings = _serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<RetrievalSettings>>()
            .Value;

        // Act & Assert
        Should.NotThrow(() => retrievalSettings.Validate());
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
