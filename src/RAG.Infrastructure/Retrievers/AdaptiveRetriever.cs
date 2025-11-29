using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Domain;
using RAG.Core.Domain.Enums;
using RAG.Core.Enums;
using System.Diagnostics;

namespace RAG.Infrastructure.Retrievers;

/// <summary>
/// Adaptive retrieval strategy that selects the optimal retrieval method based on query classification.
/// Routes queries to BM25, Dense, or Hybrid retrievers based on QueryType analysis for maximum relevance.
/// </summary>
public class AdaptiveRetriever : IRetrievalStrategy
{
    private readonly IQueryClassifier _queryClassifier;
    private readonly IRetriever _bm25Retriever;
    private readonly IRetriever _denseRetriever;
    private readonly IRetriever _hybridRetriever;
    private readonly ILogger<AdaptiveRetriever> _logger;

    public AdaptiveRetriever(
        IQueryClassifier queryClassifier,
        IRetriever bm25Retriever,
        IRetriever denseRetriever,
        IRetriever hybridRetriever,
        ILogger<AdaptiveRetriever> logger)
    {
        _queryClassifier = queryClassifier ?? throw new ArgumentNullException(nameof(queryClassifier));
        _bm25Retriever = bm25Retriever ?? throw new ArgumentNullException(nameof(bm25Retriever));
        _denseRetriever = denseRetriever ?? throw new ArgumentNullException(nameof(denseRetriever));
        _hybridRetriever = hybridRetriever ?? throw new ArgumentNullException(nameof(hybridRetriever));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation("AdaptiveRetriever initialized with query-aware strategy selection");
    }

    /// <inheritdoc />
    public string GetStrategyName() => "Adaptive";

    /// <inheritdoc />
    public RetrievalStrategyType StrategyType => RetrievalStrategyType.Adaptive;

    /// <summary>
    /// Searches for relevant documents using query-aware strategy selection.
    /// </summary>
    /// <param name="query">The search query text.</param>
    /// <param name="topK">The number of top results to return.</param>
    /// <param name="tenantId">The tenant ID to filter results.</param>
    /// <param name="strategyOverride">Optional manual strategy override ("bm25", "dense", or "hybrid"). Case-insensitive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of retrieval results ordered by relevance score (descending).</returns>
    public async Task<List<RetrievalResult>> SearchAsync(
        string query,
        int topK,
        Guid tenantId,
        string? strategyOverride = null,
        CancellationToken cancellationToken = default)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be null or empty", nameof(query));
        }

        if (topK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "topK must be greater than 0");
        }

        _logger.LogDebug(
            "Executing Adaptive search: query='{Query}', topK={TopK}, tenantId={TenantId}, strategyOverride={StrategyOverride}",
            query, topK, tenantId, strategyOverride ?? "none");

        var stopwatch = Stopwatch.StartNew();

        // Step 1: Determine strategy
        IRetriever selectedRetriever;
        string selectedStrategyName;

        if (!string.IsNullOrWhiteSpace(strategyOverride))
        {
            // Manual override path
            selectedRetriever = GetRetrieverByName(strategyOverride);
            selectedStrategyName = strategyOverride.ToLower();

            _logger.LogInformation(
                "Using manual strategy override: {Strategy} for query '{Query}'",
                selectedStrategyName, query);
        }
        else
        {
            // Automatic classification path
            var queryType = await _queryClassifier.ClassifyQueryAsync(query, cancellationToken);
            selectedRetriever = SelectRetrieverForQueryType(queryType);
            selectedStrategyName = GetStrategyNameForQueryType(queryType);

            _logger.LogInformation(
                "Query classified as {QueryType}, selected strategy: {Strategy} for query '{Query}'",
                queryType, selectedStrategyName, query);
        }

        // Step 2: Execute retrieval with selected strategy
        var results = await selectedRetriever.SearchAsync(query, topK, tenantId, cancellationToken);

        stopwatch.Stop();

        // Step 3: Log metrics with structured logging
        _logger.LogInformation(
            "Adaptive retrieval completed: Strategy={Strategy}, Latency={Ms}ms, ResultCount={Count}, Query='{Query}'",
            selectedStrategyName, stopwatch.ElapsedMilliseconds, results.Count, query);

        return results;
    }

    /// <summary>
    /// Standard SearchAsync implementation without strategyOverride parameter.
    /// Required by IRetriever interface.
    /// </summary>
    async Task<List<RetrievalResult>> IRetriever.SearchAsync(
        string query,
        int topK,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await SearchAsync(query, topK, tenantId, null, cancellationToken);
    }

    /// <summary>
    /// Selects the optimal retriever based on query type classification.
    /// </summary>
    /// <param name="queryType">The classified query type.</param>
    /// <returns>The selected retriever instance.</returns>
    private IRetriever SelectRetrieverForQueryType(QueryType queryType)
    {
        return queryType switch
        {
            QueryType.ExplicitFact => _bm25Retriever,
            QueryType.ImplicitFact => _hybridRetriever,
            QueryType.InterpretableRationale => _denseRetriever,
            QueryType.HiddenRationale => _denseRetriever,
            _ => throw new ArgumentException($"Unknown query type: {queryType}", nameof(queryType))
        };
    }

    /// <summary>
    /// Gets the strategy name for a given query type.
    /// </summary>
    /// <param name="queryType">The query type.</param>
    /// <returns>Strategy name in lowercase.</returns>
    private static string GetStrategyNameForQueryType(QueryType queryType)
    {
        return queryType switch
        {
            QueryType.ExplicitFact => "bm25",
            QueryType.ImplicitFact => "hybrid",
            QueryType.InterpretableRationale => "dense",
            QueryType.HiddenRationale => "dense",
            _ => throw new ArgumentException($"Unknown query type: {queryType}", nameof(queryType))
        };
    }

    /// <summary>
    /// Gets a retriever instance by strategy name.
    /// </summary>
    /// <param name="strategyName">Strategy name (case-insensitive: "bm25", "dense", or "hybrid").</param>
    /// <returns>The retriever instance.</returns>
    /// <exception cref="ArgumentException">Thrown when strategy name is invalid.</exception>
    private IRetriever GetRetrieverByName(string strategyName)
    {
        return strategyName.ToLower() switch
        {
            "bm25" => _bm25Retriever,
            "dense" => _denseRetriever,
            "hybrid" => _hybridRetriever,
            _ => throw new ArgumentException(
                $"Invalid strategy: {strategyName}. Must be 'bm25', 'dense', or 'hybrid'.",
                nameof(strategyName))
        };
    }
}
