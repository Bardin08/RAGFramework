using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Core.Configuration;
using RAG.Core.Domain;
using RAG.Infrastructure.Retrievers;
using Shouldly;
using Testcontainers.Elasticsearch;

namespace RAG.Tests.Integration.Retrievers;

public class BM25RetrieverIntegrationTests : IAsyncLifetime
{
    private ElasticsearchContainer? _elasticsearchContainer;
    private BM25Retriever? _retriever;
    private ElasticsearchClient? _esClient;
    private const string TestIndexName = "test-documents";
    private readonly Guid _testTenantId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        // Start Elasticsearch container
        _elasticsearchContainer = new ElasticsearchBuilder()
            .WithImage("docker.elastic.co/elasticsearch/elasticsearch:8.11.0")
            .Build();

        await _elasticsearchContainer.StartAsync();

        // Configure Elasticsearch client
        var connectionSettings = new ElasticsearchClientSettings(new Uri(_elasticsearchContainer.GetConnectionString()))
            .ServerCertificateValidationCallback((sender, certificate, chain, sslPolicyErrors) => true);

        _esClient = new ElasticsearchClient(connectionSettings);

        // Create test index with BM25 similarity (default in Elasticsearch 8.x)
        var createIndexResponse = await _esClient.Indices.CreateAsync(TestIndexName, c => c
            .Settings(s => s
                .NumberOfShards(1)
                .NumberOfReplicas(0))
            .Mappings(m => m
                .Properties<DocumentChunk>(p => p
                    .Keyword(k => k.Id)
                    .Keyword(k => k.DocumentId)
                    .Keyword(k => k.TenantId)
                    .Text(k => k.Text, t => t.Analyzer("standard"))
                    .IntegerNumber(k => k.StartIndex)
                    .IntegerNumber(k => k.EndIndex)
                    .IntegerNumber(k => k.ChunkIndex)
                    .Object(k => k.Metadata))));

        createIndexResponse.IsValidResponse.ShouldBeTrue(createIndexResponse.DebugInformation);

        // Seed test documents
        await SeedTestDocuments();

        // Wait for indexing to complete
        await Task.Delay(1000);

        // Create BM25Retriever instance
        var bm25Settings = new BM25Settings
        {
            K1 = 1.2,
            B = 0.75,
            DefaultTopK = 10,
            MaxTopK = 100,
            TimeoutSeconds = 5,
            HighlightFragmentSize = 150
        };

        var elasticsearchSettings = new ElasticsearchSettings
        {
            Url = _elasticsearchContainer.GetConnectionString(),
            IndexName = TestIndexName,
            Username = "",
            Password = "",
            NumberOfShards = 1,
            NumberOfReplicas = 0
        };

        var logger = new LoggerFactory().CreateLogger<BM25Retriever>();

        _retriever = new BM25Retriever(
            Options.Create(bm25Settings),
            Options.Create(elasticsearchSettings),
            logger);
    }

    public async Task DisposeAsync()
    {
        if (_elasticsearchContainer != null)
        {
            await _elasticsearchContainer.DisposeAsync();
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
            result.Score.ShouldBeGreaterThan(0.0);
            result.Text.ShouldNotBeNullOrEmpty();
            result.Source.ShouldNotBeNullOrEmpty();
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
                $"Result at index {i} (score: {results[i].Score}) should have higher or equal score than result at index {i + 1} (score: {results[i + 1].Score})");
        }
    }

    [Fact]
    public async Task SearchAsync_WithHighlighting_ReturnsHighlightedText()
    {
        // Arrange
        var query = "natural language";
        var topK = 5;

        // Act
        var results = await _retriever!.SearchAsync(query, topK, _testTenantId);

        // Assert
        results.ShouldNotBeEmpty();

        var resultsWithHighlights = results.Where(r => !string.IsNullOrEmpty(r.HighlightedText)).ToList();
        resultsWithHighlights.ShouldNotBeEmpty();

        foreach (var result in resultsWithHighlights)
        {
            result.HighlightedText.ShouldContain("<em>");
            result.HighlightedText.ShouldContain("</em>");
        }
    }

    [Fact]
    public async Task SearchAsync_WithNoMatches_ReturnsEmptyList()
    {
        // Arrange
        var query = "xyzabc123nonexistent";
        var topK = 10;

        // Act
        var results = await _retriever!.SearchAsync(query, topK, _testTenantId);

        // Assert
        results.ShouldNotBeNull();
        results.ShouldBeEmpty();
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
    public async Task SearchAsync_Performance_CompletesUnder100ms()
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

        p95.ShouldBeLessThan(100, $"P95 latency: {p95}ms, Average: {avgTime}ms");
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

    private async Task SeedTestDocuments()
    {
        var testDocuments = new List<DocumentChunk>
        {
            CreateTestChunk("Machine learning is a subset of artificial intelligence that enables systems to learn from data.", "ml-intro.pdf"),
            CreateTestChunk("Natural language processing deals with the interaction between computers and human language.", "nlp-basics.pdf"),
            CreateTestChunk("Retrieval augmented generation combines information retrieval with text generation.", "rag-overview.pdf"),
            CreateTestChunk("Document processing involves extracting structured information from unstructured text.", "doc-processing.pdf"),
            CreateTestChunk("Vector databases store and query high-dimensional embeddings efficiently.", "vector-db.pdf"),
            CreateTestChunk("BM25 is a ranking function used in information retrieval for keyword-based search.", "bm25-algo.pdf"),
            CreateTestChunk("Semantic search uses meaning and context rather than exact keyword matching.", "semantic-search.pdf"),
            CreateTestChunk("Text chunking breaks documents into smaller segments for better retrieval.", "chunking-strategies.pdf"),
            CreateTestChunk("Elasticsearch provides full-text search capabilities using inverted indexes.", "elasticsearch-guide.pdf"),
            CreateTestChunk("Query processing involves analyzing and transforming user queries for search engines.", "query-processing.pdf")
        };

        var bulkResponse = await _esClient!.BulkAsync(b => b
            .Index(TestIndexName)
            .IndexMany(testDocuments));

        bulkResponse.IsValidResponse.ShouldBeTrue(bulkResponse.DebugInformation);
        bulkResponse.Errors.ShouldBeFalse();

        // Refresh index to make documents searchable immediately
        var refreshResponse = await _esClient.Indices.RefreshAsync(TestIndexName);
        refreshResponse.IsValidResponse.ShouldBeTrue();
    }

    private DocumentChunk CreateTestChunk(string text, string source)
    {
        var documentId = Guid.NewGuid();
        return new DocumentChunk(
            id: Guid.NewGuid(),
            documentId: documentId,
            text: text,
            startIndex: 0,
            endIndex: text.Length,
            chunkIndex: 0,
            tenantId: _testTenantId,
            metadata: new Dictionary<string, object>
            {
                ["source"] = source,
                ["documentId"] = documentId.ToString()
            }
        );
    }
}
