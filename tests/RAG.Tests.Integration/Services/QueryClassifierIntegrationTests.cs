using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Configuration;
using RAG.Application.Services;
using RAG.Core.Domain.Enums;
using Shouldly;

namespace RAG.Tests.Integration.Services;

/// <summary>
/// Integration tests for QueryClassifier.
/// Note: Full LLM integration tests will be in Epic 5.
/// These tests validate the heuristic fallback and caching behavior.
/// </summary>
public class QueryClassifierIntegrationTests : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<QueryClassifier> _logger;
    private readonly QueryClassifier _classifier;

    public QueryClassifierIntegrationTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<QueryClassifier>();

        var config = new QueryClassificationConfig
        {
            EnableCache = true,
            Timeout = 5000,
            FallbackToHeuristics = true
        };

        // Create classifier without LLM provider (Epic 5 not implemented yet)
        _classifier = new QueryClassifier(
            _cache,
            _logger,
            Options.Create(config),
            llmProvider: null);
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }

    [Fact]
    public async Task ClassifyQueryAsync_RealWorldExplicitFactQueries_ReturnsExplicitFact()
    {
        // Arrange & Act & Assert
        var queries = new[]
        {
            "What is machine learning?",
            "Who invented the telephone?",
            "When was Python created?",
            "Where is the Eiffel Tower located?",
            "Define Clean Architecture",
            "List the SOLID principles"
        };

        foreach (var query in queries)
        {
            var result = await _classifier.ClassifyQueryAsync(query);
            result.ShouldBe(QueryType.ExplicitFact, $"Query '{query}' should be classified as ExplicitFact");
        }
    }

    [Fact]
    public async Task ClassifyQueryAsync_RealWorldImplicitFactQueries_ReturnsImplicitFact()
    {
        // Arrange & Act & Assert
        var queries = new[]
        {
            "Why is RAG effective for question answering?",
            "How does BM25 ranking work?",
            "Explain the transformer architecture",
            "Describe the benefits of microservices"
        };

        foreach (var query in queries)
        {
            var result = await _classifier.ClassifyQueryAsync(query);
            result.ShouldBe(QueryType.ImplicitFact, $"Query '{query}' should be classified as ImplicitFact");
        }
    }

    [Fact]
    public async Task ClassifyQueryAsync_RealWorldInterpretableQueries_ReturnsInterpretableRationale()
    {
        // Arrange & Act & Assert
        var queries = new[]
        {
            "Compare BM25 and Dense retrieval methods",
            "Analyze the trade-offs between monolith and microservices",
            "Evaluate different embedding models",
            "What's the difference between supervised and unsupervised learning?"
        };

        foreach (var query in queries)
        {
            var result = await _classifier.ClassifyQueryAsync(query);
            result.ShouldBe(QueryType.InterpretableRationale, $"Query '{query}' should be classified as InterpretableRationale");
        }
    }

    [Fact]
    public async Task ClassifyQueryAsync_RealWorldHiddenRationaleQueries_ReturnsHiddenRationale()
    {
        // Arrange & Act & Assert
        var queries = new[]
        {
            "Should we use hybrid search for this use case?",
            "What's the best approach for document chunking?",
            "Would you recommend OpenAI or Ollama?",
            "Do you think Clean Architecture is worth the complexity?"
        };

        foreach (var query in queries)
        {
            var result = await _classifier.ClassifyQueryAsync(query);
            result.ShouldBe(QueryType.HiddenRationale, $"Query '{query}' should be classified as HiddenRationale");
        }
    }

    [Fact]
    public async Task ClassifyQueryAsync_PerformanceTest_ClassifiesQuickly()
    {
        // Arrange
        var queries = new[]
        {
            "What is machine learning?",
            "Why is this important?",
            "Compare two approaches",
            "Should we use this?"
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        foreach (var query in queries)
        {
            await _classifier.ClassifyQueryAsync(query);
        }

        stopwatch.Stop();

        // Assert
        // First run without cache should be fast (heuristics only)
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100, "Heuristic classification should be very fast");
    }

    [Fact]
    public async Task ClassifyQueryAsync_CachedPerformance_IsVeryFast()
    {
        // Arrange
        var query = "What is machine learning?";

        // Prime the cache
        await _classifier.ClassifyQueryAsync(query);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Call 10 times with same query
        for (int i = 0; i < 10; i++)
        {
            await _classifier.ClassifyQueryAsync(query);
        }

        stopwatch.Stop();

        // Assert
        // Cached calls should be extremely fast (< 10ms total for 10 calls)
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(10, "Cached classifications should be near-instant");
    }

    [Fact]
    public async Task ClassifyQueryAsync_MixedQueryTypes_ClassifiesCorrectly()
    {
        // Arrange
        var testCases = new Dictionary<string, QueryType>
        {
            ["What is Clean Architecture?"] = QueryType.ExplicitFact,
            ["Why use microservices?"] = QueryType.ImplicitFact,
            ["Compare SQL and NoSQL databases"] = QueryType.InterpretableRationale,
            ["Should we migrate to the cloud?"] = QueryType.HiddenRationale,
            ["Who created Python?"] = QueryType.ExplicitFact,
            ["How does caching improve performance?"] = QueryType.ImplicitFact,
            ["Analyze REST vs GraphQL"] = QueryType.InterpretableRationale,
            ["What's the best database?"] = QueryType.HiddenRationale
        };

        // Act & Assert
        foreach (var (query, expectedType) in testCases)
        {
            var result = await _classifier.ClassifyQueryAsync(query);
            result.ShouldBe(expectedType, $"Query '{query}' should be classified as {expectedType}");
        }
    }

    [Fact]
    public async Task ClassifyQueryAsync_AccuracyOnTestSet_MeetsThreshold()
    {
        // Arrange - Manually labeled test set (ground truth)
        var testSet = new Dictionary<string, QueryType>
        {
            // Explicit Fact (10 queries)
            ["What is machine learning?"] = QueryType.ExplicitFact,
            ["Who invented RAG?"] = QueryType.ExplicitFact,
            ["When was .NET released?"] = QueryType.ExplicitFact,
            ["Where is Elasticsearch hosted?"] = QueryType.ExplicitFact,
            ["Define vector database"] = QueryType.ExplicitFact,
            ["List retrieval strategies"] = QueryType.ExplicitFact,
            ["Name the components"] = QueryType.ExplicitFact,
            ["What are embeddings?"] = QueryType.ExplicitFact,
            ["Show me the formula"] = QueryType.ExplicitFact,
            ["Tell me about BM25"] = QueryType.ExplicitFact,

            // Implicit Fact (10 queries)
            ["Why is caching important?"] = QueryType.ImplicitFact,
            ["How does BM25 work?"] = QueryType.ImplicitFact,
            ["Explain transformers"] = QueryType.ImplicitFact,
            ["Describe the process"] = QueryType.ImplicitFact,
            ["Why use embeddings?"] = QueryType.ImplicitFact,
            ["How to implement RAG?"] = QueryType.ImplicitFact,
            ["Explain the architecture"] = QueryType.ImplicitFact,
            ["Describe the algorithm"] = QueryType.ImplicitFact,
            ["Why prefer Dense over BM25?"] = QueryType.ImplicitFact,
            ["How does reranking help?"] = QueryType.ImplicitFact,

            // Interpretable Rationale (10 queries)
            ["Compare BM25 and Dense"] = QueryType.InterpretableRationale,
            ["Analyze the trade-offs"] = QueryType.InterpretableRationale,
            ["Evaluate different models"] = QueryType.InterpretableRationale,
            ["Difference between X and Y"] = QueryType.InterpretableRationale,
            ["Compare approaches"] = QueryType.InterpretableRationale,
            ["Analyze pros and cons"] = QueryType.InterpretableRationale,
            ["Evaluate performance"] = QueryType.InterpretableRationale,
            ["Contrast methods"] = QueryType.InterpretableRationale,
            ["Compare effectiveness"] = QueryType.InterpretableRationale,
            ["Analyze the results"] = QueryType.InterpretableRationale,

            // Hidden Rationale (10 queries)
            ["Should we use hybrid search?"] = QueryType.HiddenRationale,
            ["What's the best approach?"] = QueryType.HiddenRationale,
            ["Would you recommend this?"] = QueryType.HiddenRationale,
            ["Do you think it's good?"] = QueryType.HiddenRationale,
            ["Should I choose X or Y?"] = QueryType.HiddenRationale,
            ["Better option for this?"] = QueryType.HiddenRationale,
            ["What do you prefer?"] = QueryType.HiddenRationale,
            ["Suggest a solution"] = QueryType.HiddenRationale,
            ["Opinion on this approach?"] = QueryType.HiddenRationale,
            ["Best practice for this?"] = QueryType.HiddenRationale
        };

        int correct = 0;
        int total = testSet.Count;

        // Act
        foreach (var (query, expectedType) in testSet)
        {
            var result = await _classifier.ClassifyQueryAsync(query);
            if (result == expectedType)
            {
                correct++;
            }
        }

        // Assert
        var accuracy = (double)correct / total;

        // Heuristic classifier should achieve at least 80% accuracy
        // (AC #8 specifies 80% minimum for integration tests)
        accuracy.ShouldBeGreaterThanOrEqualTo(0.80,
            $"Classifier accuracy is {accuracy:P}, expected >= 80%. {correct}/{total} correct");
    }
}
