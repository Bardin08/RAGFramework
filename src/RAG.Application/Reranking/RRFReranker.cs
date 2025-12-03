using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Core.Configuration;
using RAG.Core.Domain;
using System.Diagnostics;

namespace RAG.Application.Reranking;

/// <summary>
/// Reciprocal Rank Fusion (RRF) reranking implementation.
/// Combines results from multiple retrieval sources using the RRF formula: RRF(d) = Σ 1 / (k + rank(d)).
/// Based on research by Cormack, Clarke, Buettcher (SIGIR 2009).
/// </summary>
/// <remarks>
/// RRF is a simple yet effective fusion method that:
/// - Assigns scores based on document rank position in each result set
/// - Sums scores for documents appearing in multiple sources
/// - Does not require score normalization across different retrieval methods
/// - Has been shown to outperform Condorcet and individual rank learning methods
///
/// Example calculation:
/// Document A: rank 1 in BM25, rank 3 in Dense (k=60)
///   RRF = 1/(60+1) + 1/(60+3) = 0.0164 + 0.0159 = 0.0323
/// Document B: rank 2 in BM25 only (k=60)
///   RRF = 1/(60+2) = 0.0161
/// Final ranking: A (0.0323) > B (0.0161)
///
/// Reference: Cormack, G. V., Clarke, C. L., and Buettcher, S. (2009).
/// "Reciprocal Rank Fusion outperforms Condorcet and individual rank learning methods."
/// Proceedings of SIGIR 2009.
/// </remarks>
public class RRFReranker : IRRFReranker
{
    private readonly RRFConfig _config;
    private readonly ILogger<RRFReranker> _logger;

    /// <summary>
    /// Initializes a new instance of the RRFReranker class.
    /// </summary>
    /// <param name="config">RRF configuration options (contains k parameter).</param>
    /// <param name="logger">Logger for structured logging.</param>
    /// <exception cref="ArgumentNullException">Thrown if config or logger is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if configuration validation fails.</exception>
    public RRFReranker(IOptions<RRFConfig> config, ILogger<RRFReranker> logger)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate configuration on startup
        _config.Validate();

        _logger.LogInformation(
            "RRFReranker initialized with K={K}",
            _config.K);
    }

    /// <inheritdoc />
    public List<RetrievalResult> Rerank(List<List<RetrievalResult>> resultSets, int topK)
    {
        // Input validation
        if (resultSets == null)
        {
            throw new ArgumentNullException(nameof(resultSets));
        }

        if (topK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "topK must be greater than 0");
        }

        // Edge case: empty result sets
        if (resultSets.Count == 0 || resultSets.All(set => set.Count == 0))
        {
            _logger.LogWarning("RRF reranking called with empty result sets");
            return new List<RetrievalResult>();
        }

        var stopwatch = Stopwatch.StartNew();

        // Step 1: Build document score dictionary (DocumentId → RetrievalResult + RRF score)
        var documentScores = new Dictionary<Guid, (RetrievalResult Result, double RRFScore)>();

        // Step 2: Process each result set and calculate RRF contributions
        for (int setIndex = 0; setIndex < resultSets.Count; setIndex++)
        {
            var results = resultSets[setIndex];

            // Assign RRF score based on rank in this set (1-indexed rank)
            for (int rank = 0; rank < results.Count; rank++)
            {
                var result = results[rank];
                var rrfScore = CalculateRRFScore(rank + 1); // 1-indexed: rank 0 → position 1

                if (documentScores.ContainsKey(result.DocumentId))
                {
                    // Document appears in multiple sets - sum RRF scores
                    var existing = documentScores[result.DocumentId];
                    documentScores[result.DocumentId] = (existing.Result, existing.RRFScore + rrfScore);
                }
                else
                {
                    // First occurrence of this document
                    documentScores[result.DocumentId] = (result, rrfScore);
                }
            }
        }

        // Step 3: Sort by RRF score (descending) and take top K
        var reranked = documentScores.Values
            .OrderByDescending(x => x.RRFScore)
            .Take(topK)
            .Select(x =>
            {
                // Return new RetrievalResult with updated Score (RRF score)
                // Use 'with' expression for record type
                return x.Result with { Score = x.RRFScore };
            })
            .ToList();

        stopwatch.Stop();

        _logger.LogInformation(
            "RRF reranking completed in {ElapsedMs}ms: {TotalDocs} unique documents → {FinalDocs} top-K results (K={TopK})",
            stopwatch.ElapsedMilliseconds,
            documentScores.Count,
            reranked.Count,
            topK);

        // Performance warning if exceeds target
        if (stopwatch.ElapsedMilliseconds > 10)
        {
            _logger.LogWarning(
                "RRF reranking exceeded performance target (10ms): {ElapsedMs}ms for {TotalDocs} documents",
                stopwatch.ElapsedMilliseconds,
                documentScores.Count);
        }

        return reranked;
    }

    /// <summary>
    /// Calculates RRF score for a given rank position.
    /// </summary>
    /// <param name="rank">1-indexed rank position (1 = top-ranked document).</param>
    /// <returns>RRF score using formula: 1 / (k + rank).</returns>
    private double CalculateRRFScore(int rank)
    {
        // Formula: 1 / (k + rank)
        // k=60 is empirically validated optimal value from research
        // Lower k values emphasize top ranks more, higher k values smooth differences
        return 1.0 / (_config.K + rank);
    }
}
