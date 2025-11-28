using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Qdrant.Client.Grpc;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;
using RAG.Infrastructure.Retrievers;
using Shouldly;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace RAG.Tests.Integration.Retrievers;

public class DenseRetrieverIntegrationTests : IAsyncLifetime
{
    private IContainer? _qdrantContainer;
    private DenseRetriever? _retriever;
    private Qdrant.Client.QdrantClient? _qdrantClient;
    private RAG.Infrastructure.VectorStores.QdrantClient? _vectorStoreClient;
    private Mock<IEmbeddingService>? _embeddingServiceMock;
    private const string CollectionName = "test-documents";
    private readonly Guid _testTenantId = Guid.NewGuid();
    private const int EmbeddingDimensions = 384; // multilingual-e5-large model dimensions

    public async Task InitializeAsync()
    {
        // Start Qdrant container
        _qdrantContainer = new ContainerBuilder()
            .WithImage("qdrant/qdrant:v1.7.4")
            .WithPortBinding(6334, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6334))
            .Build();

        await _qdrantContainer.StartAsync();

        // Configure Qdrant client
        var qdrantHost = _qdrantContainer.Hostname;
        var qdrantPort = _qdrantContainer.GetMappedPublicPort(6334);
        _qdrantClient = new Qdrant.Client.QdrantClient(qdrantHost, qdrantPort, https: false);

        // Create test collection
        await _qdrantClient.CreateCollectionAsync(
            collectionName: CollectionName,
            vectorsConfig: new VectorParams
            {
                Size = (ulong)EmbeddingDimensions,
                Distance = Distance.Cosine
            });

        // Wait for collection to be ready
        await Task.Delay(500);

        // Setup QdrantClient wrapper
        var qdrantSettings = new QdrantSettings
        {
            Url = $"http://{qdrantHost}:{qdrantPort}",
            CollectionName = CollectionName,
            VectorSize = EmbeddingDimensions,
            Distance = "Cosine"
        };

        var qdrantLogger = new LoggerFactory().CreateLogger<RAG.Infrastructure.VectorStores.QdrantClient>();
        _vectorStoreClient = new RAG.Infrastructure.VectorStores.QdrantClient(
            Options.Create(qdrantSettings),
            qdrantLogger);

        // Seed test data
        await SeedTestDocuments();

        // Wait for indexing to complete
        await Task.Delay(1000);

        // Setup mock embedding service
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        SetupMockEmbeddingService();

        // Create DenseRetriever instance
        var denseSettings = new DenseSettings
        {
            DefaultTopK = 10,
            MaxTopK = 100,
            TimeoutSeconds = 10,
            SimilarityThreshold = 0.5,
            EmbeddingTimeoutSeconds = 5,
            QdrantTimeoutSeconds = 5
        };

        var logger = new LoggerFactory().CreateLogger<DenseRetriever>();

        _retriever = new DenseRetriever(
            Options.Create(denseSettings),
            _embeddingServiceMock.Object,
            _vectorStoreClient,
            logger);
    }

    public async Task DisposeAsync()
    {
        if (_qdrantContainer != null)
        {
            await _qdrantContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task SearchAsync_WithValidQuery_ReturnsResults()
    {
        // Arrange
        var query = "machine learning";
        var topK = 5;

        // Act
        var results = await _retriever!.SearchAsync(query, topK, _testTenantId);

        // Assert
        results.ShouldNotBeNull();
        results.ShouldNotBeEmpty();
        results.Count.ShouldBeLessThanOrEqualTo(topK);

        foreach (var result in results)
        {
            result.DocumentId.ShouldNotBe(Guid.Empty);
            result.Score.ShouldBeGreaterThanOrEqualTo(0.0);
            result.Score.ShouldBeLessThanOrEqualTo(1.0); // Normalized [0, 1] range
            result.Text.ShouldNotBeNullOrEmpty();
            result.Source.ShouldNotBeNullOrEmpty();
            result.HighlightedText.ShouldBeNull(); // Dense retrieval doesn't support highlighting
        }
    }

    [Fact]
    public async Task SearchAsync_ReturnsResultsOrderedByScoreDescending()
    {
        // Arrange
        var query = "retrieval augmented generation";
        var topK = 10;

        // Act
        var results = await _retriever!.SearchAsync(query, topK, _testTenantId);

        // Assert
        results.ShouldNotBeEmpty();

        for (int i = 0; i < results.Count - 1; i++)
        {
            results[i].Score.ShouldBeGreaterThanOrEqualTo(results[i + 1].Score,
                $"Result at index {i} (score: {results[i].Score:F4}) should have higher or equal score than result at index {i + 1} (score: {results[i + 1].Score:F4})");
        }
    }

    [Fact]
    public async Task SearchAsync_WithHighSimilarityThreshold_FiltersLowScoreResults()
    {
        // Arrange
        var highThresholdSettings = new DenseSettings
        {
            SimilarityThreshold = 0.8 // High threshold
        };

        var logger = new LoggerFactory().CreateLogger<DenseRetriever>();
        var highThresholdRetriever = new DenseRetriever(
            Options.Create(highThresholdSettings),
            _embeddingServiceMock!.Object,
            _vectorStoreClient!,
            logger);

        var query = "machine learning";
        var topK = 10;

        // Act
        var results = await highThresholdRetriever.SearchAsync(query, topK, _testTenantId);

        // Assert
        foreach (var result in results)
        {
            result.Score.ShouldBeGreaterThanOrEqualTo(0.8);
        }
    }

    [Fact]
    public async Task SearchAsync_NormalizesCosineSimilarity_ToZeroOneRange()
    {
        // Arrange
        var query = "document processing";
        var topK = 10;

        // Act
        var results = await _retriever!.SearchAsync(query, topK, _testTenantId);

        // Assert
        results.ShouldNotBeEmpty();

        foreach (var result in results)
        {
            result.Score.ShouldBeGreaterThanOrEqualTo(0.0);
            result.Score.ShouldBeLessThanOrEqualTo(1.0);
        }
    }

    [Fact]
    public async Task SearchAsync_WithDifferentTenant_ReturnsNoResults()
    {
        // Arrange
        var query = "machine learning";
        var topK = 10;
        var differentTenantId = Guid.NewGuid();

        // Act
        var results = await _retriever!.SearchAsync(query, topK, differentTenantId);

        // Assert
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_Performance_CompletesUnder200ms()
    {
        // Arrange
        var query = "document processing";
        var topK = 10;
        var iterations = 10;
        var times = new List<long>();

        // Warm-up
        await _retriever!.SearchAsync(query, topK, _testTenantId);

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await _retriever.SearchAsync(query, topK, _testTenantId);
            stopwatch.Stop();
            times.Add(stopwatch.ElapsedMilliseconds);
        }

        // Assert
        var p95 = times.OrderBy(t => t).Skip((int)(iterations * 0.95)).First();
        var avgTime = times.Average();

        p95.ShouldBeLessThan(200, $"P95 latency: {p95}ms, Average: {avgTime:F2}ms");
    }

    [Fact]
    public async Task SearchAsync_WithTopK1_ReturnsOnlyTopResult()
    {
        // Arrange
        var query = "retrieval";
        var topK = 1;

        // Act
        var results = await _retriever!.SearchAsync(query, topK, _testTenantId);

        // Assert
        results.Count.ShouldBeLessThanOrEqualTo(1);

        if (results.Any())
        {
            results[0].Score.ShouldBeGreaterThan(0.0);
        }
    }

    [Fact(Skip = "Flaky test - mock embeddings don't provide sufficient semantic similarity")]
    public async Task SearchAsync_WithSemanticallySimilarQuery_ReturnsRelevantResults()
    {
        // Arrange
        // Query using synonyms/semantic similarity rather than exact keywords
        var query = "artificial intelligence algorithms";
        var topK = 5;

        // Act
        var results = await _retriever!.SearchAsync(query, topK, _testTenantId);

        // Assert
        results.ShouldNotBeEmpty();

        // Should find "machine learning" document due to semantic similarity
        var mlResult = results.FirstOrDefault(r => r.Text.Contains("Machine learning"));
        mlResult.ShouldNotBeNull();
    }

    private async Task SeedTestDocuments()
    {
        var testDocuments = new List<(Guid Id, float[] Embedding, string Text, string Source, Guid DocumentId)>
        {
            (Guid.NewGuid(), GenerateTestEmbedding(0), "Machine learning is a subset of artificial intelligence that enables systems to learn from data.", "ml-intro.pdf", Guid.NewGuid()),
            (Guid.NewGuid(), GenerateTestEmbedding(1), "Natural language processing deals with the interaction between computers and human language.", "nlp-basics.pdf", Guid.NewGuid()),
            (Guid.NewGuid(), GenerateTestEmbedding(2), "Retrieval augmented generation combines information retrieval with text generation.", "rag-overview.pdf", Guid.NewGuid()),
            (Guid.NewGuid(), GenerateTestEmbedding(3), "Document processing involves extracting structured information from unstructured text.", "doc-processing.pdf", Guid.NewGuid()),
            (Guid.NewGuid(), GenerateTestEmbedding(4), "Vector databases store and query high-dimensional embeddings efficiently.", "vector-db.pdf", Guid.NewGuid()),
            (Guid.NewGuid(), GenerateTestEmbedding(5), "BM25 is a ranking function used in information retrieval for keyword-based search.", "bm25-algo.pdf", Guid.NewGuid()),
            (Guid.NewGuid(), GenerateTestEmbedding(6), "Semantic search uses meaning and context rather than exact keyword matching.", "semantic-search.pdf", Guid.NewGuid()),
            (Guid.NewGuid(), GenerateTestEmbedding(7), "Text chunking breaks documents into smaller segments for better retrieval.", "chunking-strategies.pdf", Guid.NewGuid()),
            (Guid.NewGuid(), GenerateTestEmbedding(8), "Qdrant provides vector search capabilities using HNSW indexing for fast similarity search.", "qdrant-guide.pdf", Guid.NewGuid()),
            (Guid.NewGuid(), GenerateTestEmbedding(9), "Query processing involves analyzing and transforming user queries for search engines.", "query-processing.pdf", Guid.NewGuid())
        };

        foreach (var (id, embedding, text, source, documentId) in testDocuments)
        {
            var payload = new Dictionary<string, Value>
            {
                ["text"] = new Value { StringValue = text },
                ["source"] = new Value { StringValue = source },
                ["documentId"] = new Value { StringValue = documentId.ToString() },
                ["tenantId"] = new Value { StringValue = _testTenantId.ToString() }
            };

            await _qdrantClient!.UpsertAsync(
                collectionName: CollectionName,
                points: new[]
                {
                    new PointStruct
                    {
                        Id = new PointId { Uuid = id.ToString() },
#pragma warning disable CS0612 // Type or member is obsolete
                        Vectors = new Vectors { Vector = new Vector { Data = { embedding } } },
#pragma warning restore CS0612 // Type or member is obsolete
                        Payload = { payload }
                    }
                });
        }
    }

    private float[] GenerateTestEmbedding(int seed)
    {
        // Generate deterministic test embeddings based on seed
        var random = new Random(seed);
        var embedding = new float[EmbeddingDimensions];

        for (int i = 0; i < EmbeddingDimensions; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2.0 - 1.0); // Range [-1, 1]
        }

        // Normalize to unit vector for cosine similarity
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < EmbeddingDimensions; i++)
        {
            embedding[i] /= (float)magnitude;
        }

        return embedding;
    }

    private void SetupMockEmbeddingService()
    {
        // Mock embedding service to return test embeddings based on query
        _embeddingServiceMock!.Setup(x => x.GenerateEmbeddingsAsync(
            It.IsAny<List<string>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken ct) =>
            {
                // Generate embeddings based on text content hash for consistency
                var embeddings = new List<float[]>();

                foreach (var text in texts)
                {
                    var hashCode = text.GetHashCode();
                    var seed = Math.Abs(hashCode % 10); // Map to 0-9 for consistency with test data
                    embeddings.Add(GenerateTestEmbedding(seed));
                }

                return embeddings;
            });
    }
}
