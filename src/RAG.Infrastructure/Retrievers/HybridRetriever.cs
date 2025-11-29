using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Interfaces;
using RAG.Application.Reranking;
using RAG.Core.Configuration;
using RAG.Core.Domain;
using RAG.Core.Enums;
using System.Diagnostics;

namespace RAG.Infrastructure.Retrievers;

/// <summary>
/// Hybrid retrieval strategy combining BM25 (keyword-based) and Dense (semantic) retrieval.
/// Executes both strategies in parallel and combines results with configurable weighted scoring.
/// </summary>
public class HybridRetriever : IRetrievalStrategy
{
    private readonly IRetriever _bm25Retriever;
    private readonly IRetriever _denseRetriever;
    private readonly IRRFReranker _rrfReranker;
    private readonly HybridSearchConfig _config;
    private readonly ILogger<HybridRetriever> _logger;

    // Track result counts for metadata (set during last SearchAsync call)
    public int LastBM25ResultCount { get; private set; }
    public int LastDenseResultCount { get; private set; }

    public HybridRetriever(
        IRetriever bm25Retriever,
        IRetriever denseRetriever,
        IRRFReranker rrfReranker,
        IOptions<HybridSearchConfig> config,
        ILogger<HybridRetriever> logger)
    {
        _bm25Retriever = bm25Retriever ?? throw new ArgumentNullException(nameof(bm25Retriever));
        _denseRetriever = denseRetriever ?? throw new ArgumentNullException(nameof(denseRetriever));
        _rrfReranker = rrfReranker ?? throw new ArgumentNullException(nameof(rrfReranker));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate configuration on startup
        _config.Validate();

        _logger.LogInformation(
            "HybridRetriever initialized. Alpha={Alpha}, Beta={Beta}, IntermediateK={IntermediateK}, RerankingMethod={RerankingMethod}",
            _config.Alpha, _config.Beta, _config.IntermediateK, _config.RerankingMethod);
    }

    /// <inheritdoc />
    public string GetStrategyName() => "Hybrid";

    /// <inheritdoc />
    public RetrievalStrategyType StrategyType => RetrievalStrategyType.Hybrid;

    /// <inheritdoc />
    public async Task<List<RetrievalResult>> SearchAsync(
        string query,
        int topK,
        Guid tenantId,
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
            "Executing Hybrid search: query='{Query}', topK={TopK}, tenantId={TenantId}, intermediateK={IntermediateK}",
            query, topK, tenantId, _config.IntermediateK);

        var stopwatch = Stopwatch.StartNew();

        // Step 1: Execute both retrievers in parallel with intermediateK
        List<RetrievalResult> bm25Results = new();
        List<RetrievalResult> denseResults = new();

        var bm25Task = ExecuteRetrieverSafe(
            () => _bm25Retriever.SearchAsync(query, _config.IntermediateK, tenantId, cancellationToken),
            "BM25");

        var denseTask = ExecuteRetrieverSafe(
            () => _denseRetriever.SearchAsync(query, _config.IntermediateK, tenantId, cancellationToken),
            "Dense");

        await Task.WhenAll(bm25Task, denseTask);

        bm25Results = await bm25Task;
        denseResults = await denseTask;

        // Store counts before deduplication for metadata
        LastBM25ResultCount = bm25Results.Count;
        LastDenseResultCount = denseResults.Count;

        _logger.LogInformation(
            "Parallel retrieval completed. BM25: {Bm25Count} results, Dense: {DenseCount} results",
            bm25Results.Count, denseResults.Count);

        // Step 2: Apply reranking based on configuration
        List<RetrievalResult> finalResults;

        if (string.Equals(_config.RerankingMethod, "RRF", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Using RRF reranking for result fusion");

            // RRF path: Prepare result sets for RRF (order matters: rank 1 = first element)
            var resultSets = new List<List<RetrievalResult>>
            {
                bm25Results,  // BM25 results (already ranked by score)
                denseResults  // Dense results (already ranked by score)
            };

            // RRF reranking (handles deduplication, scoring, sorting, topK internally)
            finalResults = _rrfReranker.Rerank(resultSets, topK);

            _logger.LogInformation(
                "RRF reranking completed. Final results: {Count}",
                finalResults.Count);
        }
        else // Default: "Weighted"
        {
            _logger.LogDebug(
                "Using weighted scoring for result fusion (alpha={Alpha}, beta={Beta})",
                _config.Alpha, _config.Beta);

            // Weighted path: Existing weighted scoring logic
            var normalizedBm25 = NormalizeBM25Scores(bm25Results);
            var combinedResults = CombineResults(normalizedBm25, denseResults);
            var deduplicated = DeduplicateResults(combinedResults);

            finalResults = deduplicated
                .OrderByDescending(r => r.Score)
                .Take(topK)
                .ToList();
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Hybrid retrieval completed in {ElapsedMs}ms using {Method} method, returned {Count} results (topK={TopK})",
            stopwatch.ElapsedMilliseconds, _config.RerankingMethod, finalResults.Count, topK);

        return finalResults;
    }

    /// <summary>
    /// Executes a retriever with exception handling.
    /// If retriever fails, logs error and returns empty list to continue with available results.
    /// </summary>
    private async Task<List<RetrievalResult>> ExecuteRetrieverSafe(
        Func<Task<List<RetrievalResult>>> retrieverTask,
        string retrieverName)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var results = await retrieverTask();
            sw.Stop();
            _logger.LogDebug("{RetrieverName} completed in {Ms}ms", retrieverName, sw.ElapsedMilliseconds);
            return results;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "{RetrieverName} retrieval failed after {Ms}ms. Continuing with results from other retriever.",
                retrieverName, sw.ElapsedMilliseconds);
            return new List<RetrievalResult>();
        }
    }

    /// <summary>
    /// Normalizes BM25 scores to [0, 1] range using min-max normalization.
    /// BM25 scores are unbounded, normalization is required for fair weighted combination with Dense scores.
    /// </summary>
    private List<RetrievalResult> NormalizeBM25Scores(List<RetrievalResult> bm25Results)
    {
        if (bm25Results.Count == 0)
            return bm25Results;

        var maxScore = bm25Results.Max(r => r.Score);
        var minScore = bm25Results.Min(r => r.Score);

        // Handle edge case: all scores are identical
        if (Math.Abs(maxScore - minScore) < 0.0001)
        {
            // All scores are the same, normalize to 1.0
            return bm25Results.Select(r => r with { Score = 1.0 }).ToList();
        }

        // Min-max normalization: (score - min) / (max - min)
        return bm25Results.Select(r => r with
        {
            Score = (r.Score - minScore) / (maxScore - minScore)
        }).ToList();
    }

    /// <summary>
    /// Combines results from BM25 and Dense retrievers with weighted scoring.
    /// Formula: combined_score = alpha * bm25_normalized_score + beta * dense_score
    /// Dense scores are already normalized to [0, 1].
    /// Preserves individual BM25 and Dense scores in result metadata.
    /// </summary>
    private List<RetrievalResult> CombineResults(
        List<RetrievalResult> bm25Results,
        List<RetrievalResult> denseResults)
    {
        var combined = new Dictionary<Guid, RetrievalResult>();

        // Add BM25 results with weighted score and preserve original score
        foreach (var result in bm25Results)
        {
            var weightedScore = _config.Alpha * result.Score;
            var metadata = new Dictionary<string, object>
            {
                ["BM25Score"] = result.Score, // Store original normalized BM25 score
                ["Source"] = "BM25"
            };
            combined[result.DocumentId] = result with
            {
                Score = weightedScore,
                Metadata = metadata
            };
        }

        // Add or update with Dense results
        foreach (var result in denseResults)
        {
            var weightedScore = _config.Beta * result.Score;

            if (combined.ContainsKey(result.DocumentId))
            {
                // Document appears in both result sets
                var existing = combined[result.DocumentId];
                var metadata = existing.Metadata ?? new Dictionary<string, object>();
                metadata["DenseScore"] = result.Score; // Add Dense score to existing BM25 metadata
                metadata["Source"] = "Hybrid"; // Both sources

                combined[result.DocumentId] = existing with
                {
                    Score = existing.Score + weightedScore,
                    Metadata = metadata
                };
            }
            else
            {
                // Document only in dense results
                var metadata = new Dictionary<string, object>
                {
                    ["DenseScore"] = result.Score, // Store original Dense score
                    ["Source"] = "Dense"
                };
                combined[result.DocumentId] = result with
                {
                    Score = weightedScore,
                    Metadata = metadata
                };
            }
        }

        return combined.Values.ToList();
    }

    /// <summary>
    /// Deduplicates results by DocumentId, keeping the entry with higher combined score.
    /// This method should not be needed after CombineResults, but included for safety.
    /// Logs deduplication statistics.
    /// </summary>
    private List<RetrievalResult> DeduplicateResults(List<RetrievalResult> results)
    {
        var initialCount = results.Count;

        var deduplicated = results
            .GroupBy(r => r.DocumentId)
            .Select(g => g.OrderByDescending(r => r.Score).First())
            .ToList();

        var duplicatesRemoved = initialCount - deduplicated.Count;

        if (duplicatesRemoved > 0)
        {
            _logger.LogInformation(
                "Deduplication removed {DuplicateCount} duplicate documents (original: {Original}, final: {Final})",
                duplicatesRemoved, initialCount, deduplicated.Count);
        }

        return deduplicated;
    }
}
