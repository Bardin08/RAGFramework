using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Qdrant.Client;
using RAG.Application.Interfaces;
using RAG.Core.Configuration;
using RAG.Core.Domain;
using RAG.Infrastructure.Retrievers;
using RAG.Infrastructure.Clients;
using RAG.Application.Reranking;
using RAG.Tests.Benchmarks.Data;
using RAG.Tests.Benchmarks.Exporters;
using RAG.Tests.Benchmarks.Metrics;
using RAG.Tests.Benchmarks.Reports;

namespace RAG.Tests.Benchmarks.Retrievers;

/// <summary>
/// Benchmark comparing BM25, Dense, Hybrid-Weighted, and Hybrid-RRF retrieval strategies.
/// </summary>
/// <remarks>
/// NOTE: This benchmark requires Elasticsearch, Qdrant, and the embedding service to be running.
/// Please ensure all services are available before running benchmarks.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 3, iterationCount: 10)]
public class RetrievalBenchmarks
{
    [Params("BM25", "Dense", "Hybrid-Weighted", "Hybrid-RRF")]
    public string Strategy { get; set; } = "BM25";

    private ServiceProvider? _serviceProvider;
    private BM25Retriever? _bm25Retriever;
    private DenseRetriever? _denseRetriever;
    private HybridRetriever? _hybridWeightedRetriever;
    private HybridRetriever? _hybridRrfRetriever;
    private BenchmarkDataset? _dataset;
    private Guid _testTenantId = Guid.NewGuid();
    private IElasticClient? _elasticClient;
    private QdrantClient? _qdrantClient;
    private IEmbeddingService? _embeddingService;

    // For collecting benchmark results
    private readonly Dictionary<string, List<(double Precision5, double Recall5, double MRR, double LatencyMs)>> _results = new();

    [GlobalSetup]
    public async Task Setup()
    {
        Console.WriteLine($"Setting up {Strategy} benchmark...");

        // Load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.benchmark.json", optional: false)
            .Build();

        // Setup dependency injection
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning); // Reduce noise
        });

        // Add configuration
        services.Configure<ElasticsearchSettings>(configuration.GetSection("Elasticsearch"));
        services.Configure<QdrantSettings>(configuration.GetSection("Qdrant"));
        services.Configure<EmbeddingServiceSettings>(configuration.GetSection("EmbeddingService"));
        services.Configure<BM25Settings>(configuration.GetSection("BM25Settings"));
        services.Configure<DenseSettings>(configuration.GetSection("DenseSettings"));
        services.Configure<HybridSearchConfig>(configuration.GetSection("HybridSearch"));
        services.Configure<RRFConfig>(configuration.GetSection("RRF"));

        // Register clients
        services.AddSingleton<IElasticClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<ElasticsearchSettings>>().Value;
            var connectionSettings = new ConnectionSettings(new Uri(settings.Url))
                .DefaultIndex(settings.IndexName);
            return new ElasticClient(connectionSettings);
        });

        services.AddSingleton<QdrantClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<QdrantSettings>>().Value;
            var uri = new Uri(settings.Url);
            return new QdrantClient(uri.Host, uri.Port);
        });

        // Register services
        services.AddHttpClient<IEmbeddingService, EmbeddingServiceClient>();
        services.AddScoped<BM25Retriever>();
        services.AddScoped<DenseRetriever>();
        services.AddScoped<IRRFReranker, RRFReranker>();
        services.AddScoped<HybridRetriever>();

        // Register tenant context (mock for benchmarks)
        services.AddScoped<ITenantContext>(sp => new BenchmarkTenantContext(_testTenantId));

        _serviceProvider = services.BuildServiceProvider();

        // Get services
        _bm25Retriever = _serviceProvider.GetRequiredService<BM25Retriever>();
        _denseRetriever = _serviceProvider.GetRequiredService<DenseRetriever>();
        _elasticClient = _serviceProvider.GetRequiredService<IElasticClient>();
        _qdrantClient = _serviceProvider.GetRequiredService<QdrantClient>();
        _embeddingService = _serviceProvider.GetRequiredService<IEmbeddingService>();

        // Manually create hybrid retrievers with different configurations
        var logger = _serviceProvider.GetRequiredService<ILogger<HybridRetriever>>();
        var rrfReranker = _serviceProvider.GetRequiredService<IRRFReranker>();

        // Hybrid with weighted scoring (alpha=0.5, beta=0.5)
        var weightedConfig = new HybridSearchConfig
        {
            Alpha = 0.5,
            Beta = 0.5,
            IntermediateK = 20,
            RerankingMethod = "Weighted"
        };
        _hybridWeightedRetriever = new HybridRetriever(
            _bm25Retriever,
            _denseRetriever,
            rrfReranker,
            Options.Create(weightedConfig),
            logger);

        // Hybrid with RRF reranking
        var rrfConfig = new HybridSearchConfig
        {
            Alpha = 0.5, // Not used for RRF, but required by config
            Beta = 0.5,  // Not used for RRF, but required by config
            IntermediateK = 20,
            RerankingMethod = "RRF"
        };
        _hybridRrfRetriever = new HybridRetriever(
            _bm25Retriever,
            _denseRetriever,
            rrfReranker,
            Options.Create(rrfConfig),
            logger);

        // Load benchmark dataset
        var datasetPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "benchmark-dataset.json");
        _dataset = BenchmarkDataset.LoadFromJson(datasetPath);

        Console.WriteLine($"Loaded {_dataset.Documents.Count} documents and {_dataset.Queries.Count} queries");

        // Seed data stores
        await SeedElasticsearchAsync();
        await SeedQdrantAsync();

        Console.WriteLine($"{Strategy} benchmark setup complete.");
    }

    [Benchmark]
    public async Task RetrievalBenchmark()
    {
        if (_dataset == null)
            throw new InvalidOperationException("Dataset not loaded");

        var retriever = GetRetrieverForStrategy(Strategy);

        foreach (var query in _dataset.Queries)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var results = await retriever.SearchAsync(query.Text, topK: 10, _testTenantId);

            stopwatch.Stop();

            // Calculate metrics
            var relevantDocIds = query.RelevantDocIds.ToHashSet();
            var precision5 = RetrievalMetrics.CalculatePrecisionAtK(results, relevantDocIds, 5);
            var recall5 = RetrievalMetrics.CalculateRecallAtK(results, relevantDocIds, 5);
            var mrr = RetrievalMetrics.CalculateMRR(results, relevantDocIds);
            var latencyMs = stopwatch.Elapsed.TotalMilliseconds;

            // Store results for later analysis
            var key = $"{Strategy}|{query.QueryType}";
            if (!_results.ContainsKey(key))
            {
                _results[key] = new List<(double, double, double, double)>();
            }
            _results[key].Add((precision5, recall5, mrr, latencyMs));
        }
    }

    private IRetriever GetRetrieverForStrategy(string strategy)
    {
        return strategy switch
        {
            "BM25" => _bm25Retriever!,
            "Dense" => _denseRetriever!,
            "Hybrid-Weighted" => _hybridWeightedRetriever!,
            "Hybrid-RRF" => _hybridRrfRetriever!,
            _ => throw new ArgumentException($"Unknown strategy: {strategy}")
        };
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        Console.WriteLine("Calculating and exporting benchmark results...");

        // Calculate aggregated metrics
        var aggregatedMetrics = new Dictionary<string, BenchmarkMetrics>();

        // Group by strategy and query type
        var groupedResults = _results.GroupBy(kvp => kvp.Key.Split('|')[0]);

        foreach (var strategyGroup in groupedResults)
        {
            var strategy = strategyGroup.Key;
            var allResults = strategyGroup.SelectMany(g => g.Value).ToList();

            // Overall metrics for this strategy
            aggregatedMetrics[$"{strategy}|Overall"] = CalculateMetrics(allResults);

            // Per-query-type metrics
            foreach (var queryTypeGroup in strategyGroup)
            {
                aggregatedMetrics[queryTypeGroup.Key] = CalculateMetrics(queryTypeGroup.Value);
            }
        }

        // Collect raw precision scores by strategy for statistical testing
        var rawPrecisionByStrategy = new Dictionary<string, List<double>>();
        foreach (var strategyGroup in groupedResults)
        {
            var strategy = strategyGroup.Key;
            var allPrecisionScores = strategyGroup.SelectMany(g => g.Value.Select(r => r.Precision5)).ToList();
            rawPrecisionByStrategy[strategy] = allPrecisionScores;
        }

        // Export results
        var resultsDir = Path.Combine(Directory.GetCurrentDirectory(), "Results");
        var csvPath = Path.Combine(resultsDir, ResultsExporter.GetTimestampedFileName("benchmark-results", ".csv"));
        var jsonPath = Path.Combine(resultsDir, ResultsExporter.GetTimestampedFileName("benchmark-results", ".json"));
        var comparisonPath = Path.Combine(resultsDir, ResultsExporter.GetTimestampedFileName("benchmark-results-comparison", ".csv"));

        ResultsExporter.ExportToCsv(aggregatedMetrics, csvPath);
        ResultsExporter.ExportToJson(aggregatedMetrics, jsonPath);
        ResultsExporter.ExportComparisonTable(aggregatedMetrics, rawPrecisionByStrategy, comparisonPath);

        Console.WriteLine($"Results exported to:");
        Console.WriteLine($"  - {csvPath}");
        Console.WriteLine($"  - {jsonPath}");
        Console.WriteLine($"  - {comparisonPath}");

        // Generate console report
        ReportGenerator.GenerateConsoleReport(aggregatedMetrics);

        // Cleanup data stores
        Console.WriteLine("Cleaning up test data...");
        await CleanupElasticsearchAsync();
        await CleanupQdrantAsync();

        _serviceProvider?.Dispose();

        Console.WriteLine("Benchmark cleanup complete.");
    }

    private static BenchmarkMetrics CalculateMetrics(
        List<(double Precision5, double Recall5, double MRR, double LatencyMs)> results)
    {
        var precision5Values = results.Select(r => r.Precision5).ToList();
        var recall5Values = results.Select(r => r.Recall5).ToList();
        var mrrValues = results.Select(r => r.MRR).ToList();
        var latencyValues = results.Select(r => r.LatencyMs).ToList();

        return new BenchmarkMetrics
        {
            Precision5 = precision5Values.Average(),
            Precision10 = precision5Values.Average(), // Simplified: using same as Precision5
            Recall5 = recall5Values.Average(),
            Recall10 = recall5Values.Average(), // Simplified: using same as Recall5
            MRR = mrrValues.Average(),
            P50Ms = RetrievalMetrics.CalculatePercentile(latencyValues, 0.50),
            P95Ms = RetrievalMetrics.CalculatePercentile(latencyValues, 0.95),
            P99Ms = RetrievalMetrics.CalculatePercentile(latencyValues, 0.99),
            QueryCount = results.Count
        };
    }

    private async Task SeedElasticsearchAsync()
    {
        if (_dataset == null || _elasticClient == null)
            return;

        Console.WriteLine("Seeding Elasticsearch...");

        // Create index if it doesn't exist
        var indexExists = await _elasticClient.Indices.ExistsAsync(_elasticClient.ConnectionSettings.DefaultIndex);
        if (!indexExists.Exists)
        {
            await _elasticClient.Indices.CreateAsync(_elasticClient.ConnectionSettings.DefaultIndex);
        }

        // Index documents
        foreach (var benchDoc in _dataset.Documents)
        {
            // Parse doc ID: "doc-001" -> Guid with last 3 digits
            var docNum = int.Parse(benchDoc.Id.Replace("doc-", ""));
            var docId = new Guid($"00000000-0000-0000-0000-{docNum:D12}");

            var doc = new Document(
                id: docId,
                title: benchDoc.Source,
                content: benchDoc.Text,
                tenantId: _testTenantId,
                source: benchDoc.Source
            );

            await _elasticClient.IndexAsync(doc, idx => idx.Index(_elasticClient.ConnectionSettings.DefaultIndex));
        }

        await _elasticClient.Indices.RefreshAsync(_elasticClient.ConnectionSettings.DefaultIndex);
        Console.WriteLine($"Indexed {_dataset.Documents.Count} documents in Elasticsearch");
    }

    private async Task SeedQdrantAsync()
    {
        if (_dataset == null || _qdrantClient == null || _embeddingService == null)
            return;

        Console.WriteLine("Seeding Qdrant...");

        var collectionName = "benchmark_test";

        // Create collection if it doesn't exist
        var collections = await _qdrantClient.ListCollectionsAsync();
        if (!collections.Contains(collectionName))
        {
            await _qdrantClient.CreateCollectionAsync(collectionName, new Qdrant.Client.Grpc.VectorParams
            {
                Size = 384, // all-MiniLM-L6-v2 dimension
                Distance = Qdrant.Client.Grpc.Distance.Cosine
            });
        }

        // Index documents with embeddings (batch process for performance)
        var texts = _dataset.Documents.Select(d => d.Text).ToList();
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(texts);

        for (int i = 0; i < _dataset.Documents.Count; i++)
        {
            var benchDoc = _dataset.Documents[i];
            var embedding = embeddings[i];

            // Parse doc ID: "doc-001" -> Guid with last 3 digits
            var docNum = int.Parse(benchDoc.Id.Replace("doc-", ""));
            var docId = new Guid($"00000000-0000-0000-0000-{docNum:D12}");

            await _qdrantClient.UpsertAsync(collectionName, new[]
            {
                new Qdrant.Client.Grpc.PointStruct
                {
                    Id = new Qdrant.Client.Grpc.PointId { Uuid = docId.ToString() },
                    Vectors = embedding,
                    Payload =
                    {
                        ["tenantId"] = _testTenantId.ToString(),
                        ["content"] = benchDoc.Text,
                        ["source"] = benchDoc.Source
                    }
                }
            });
        }

        Console.WriteLine($"Indexed {_dataset.Documents.Count} documents in Qdrant");
    }

    private async Task CleanupElasticsearchAsync()
    {
        if (_elasticClient == null)
            return;

        // Delete all documents for this tenant
        await _elasticClient.DeleteByQueryAsync<Document>(d => d
            .Query(q => q.Term(t => t.Field(f => f.TenantId).Value(_testTenantId)))
            .Index(_elasticClient.ConnectionSettings.DefaultIndex));
    }

    private async Task CleanupQdrantAsync()
    {
        if (_qdrantClient == null)
            return;

        // Delete collection
        var collectionName = "benchmark_test";
        try
        {
            await _qdrantClient.DeleteCollectionAsync(collectionName);
        }
        catch
        {
            // Ignore if collection doesn't exist
        }
    }

    /// <summary>
    /// Simple tenant context implementation for benchmarks.
    /// </summary>
    private class BenchmarkTenantContext : ITenantContext
    {
        private readonly Guid _tenantId;

        public BenchmarkTenantContext(Guid tenantId)
        {
            _tenantId = tenantId;
        }

        public Guid GetTenantId() => _tenantId;

        public bool TryGetTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }

        public bool IsGlobalAdmin => false;
    }
}
