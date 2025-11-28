using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Interfaces;
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
    private readonly HybridSearchConfig _config;
    private readonly ILogger<HybridRetriever> _logger;

    public HybridRetriever(
        IRetriever bm25Retriever,
        IRetriever denseRetriever,
        IOptions<HybridSearchConfig> config,
        ILogger<HybridRetriever> logger)
    {
        _bm25Retriever = bm25Retriever ?? throw new ArgumentNullException(nameof(bm25Retriever));
        _denseRetriever = denseRetriever ?? throw new ArgumentNullException(nameof(denseRetriever));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate configuration on startup
        _config.Validate();

        _logger.LogInformation(
            "HybridRetriever initialized. Alpha={Alpha}, Beta={Beta}, IntermediateK={IntermediateK}",
            _config.Alpha, _config.Beta, _config.IntermediateK);
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

        _logger.LogInformation(
            "Parallel retrieval completed. BM25: {Bm25Count} results, Dense: {DenseCount} results",
            bm25Results.Count, denseResults.Count);

        // Step 2: Normalize scores and combine results with weighted scoring
        var normalizedBm25 = NormalizeBM25Scores(bm25Results);
        var combinedResults = CombineResults(normalizedBm25, denseResults);

        // Step 3: Deduplication by DocumentId (keep higher combined score)
        var deduplicated = DeduplicateResults(combinedResults);

        // Step 4: Sort by combined score descending and take top K
        var finalResults = deduplicated
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        stopwatch.Stop();
        _logger.LogInformation(
            "Hybrid retrieval completed in {ElapsedMs}ms, returned {Count} results (topK={TopK})",
            stopwatch.ElapsedMilliseconds, finalResults.Count, topK);

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
    /// </summary>
    private List<RetrievalResult> CombineResults(
        List<RetrievalResult> bm25Results,
        List<RetrievalResult> denseResults)
    {
        var combined = new Dictionary<Guid, RetrievalResult>();

        // Add BM25 results with weighted score
        foreach (var result in bm25Results)
        {
            var weightedScore = _config.Alpha * result.Score;
            combined[result.DocumentId] = result with { Score = weightedScore };
        }

        // Add or update with Dense results
        foreach (var result in denseResults)
        {
            var weightedScore = _config.Beta * result.Score;

            if (combined.ContainsKey(result.DocumentId))
            {
                // Document appears in both result sets - add dense weighted score to existing
                var existing = combined[result.DocumentId];
                combined[result.DocumentId] = existing with { Score = existing.Score + weightedScore };
            }
            else
            {
                // Document only in dense results
                combined[result.DocumentId] = result with { Score = weightedScore };
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
