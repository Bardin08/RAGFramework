using RAG.Core.Domain;

namespace RAG.Tests.Benchmarks.Metrics;

/// <summary>
/// Provides methods for calculating information retrieval metrics.
/// </summary>
public static class RetrievalMetrics
{
    /// <summary>
    /// Calculates Precision@K: the fraction of retrieved documents that are relevant.
    /// </summary>
    /// <param name="results">Retrieved documents ordered by relevance.</param>
    /// <param name="relevantDocIds">Set of relevant document IDs.</param>
    /// <param name="k">Number of top results to consider.</param>
    /// <returns>Precision@K value between 0.0 and 1.0.</returns>
    public static double CalculatePrecisionAtK(
        List<RetrievalResult> results,
        HashSet<string> relevantDocIds,
        int k)
    {
        if (k <= 0)
        {
            throw new ArgumentException("K must be greater than 0.", nameof(k));
        }

        if (results.Count == 0)
        {
            return 0.0;
        }

        var topK = results.Take(k).ToList();
        var relevantCount = topK.Count(r => relevantDocIds.Contains(r.DocumentId.ToString()));

        return topK.Count > 0 ? (double)relevantCount / topK.Count : 0.0;
    }

    /// <summary>
    /// Calculates Recall@K: the fraction of relevant documents that are retrieved.
    /// </summary>
    /// <param name="results">Retrieved documents ordered by relevance.</param>
    /// <param name="relevantDocIds">Set of relevant document IDs.</param>
    /// <param name="k">Number of top results to consider.</param>
    /// <returns>Recall@K value between 0.0 and 1.0.</returns>
    public static double CalculateRecallAtK(
        List<RetrievalResult> results,
        HashSet<string> relevantDocIds,
        int k)
    {
        if (k <= 0)
        {
            throw new ArgumentException("K must be greater than 0.", nameof(k));
        }

        if (relevantDocIds.Count == 0)
        {
            return 0.0;
        }

        var topK = results.Take(k).ToList();
        var relevantCount = topK.Count(r => relevantDocIds.Contains(r.DocumentId.ToString()));

        return (double)relevantCount / relevantDocIds.Count;
    }

    /// <summary>
    /// Calculates Mean Reciprocal Rank (MRR): the reciprocal of the rank of the first relevant result.
    /// </summary>
    /// <param name="results">Retrieved documents ordered by relevance.</param>
    /// <param name="relevantDocIds">Set of relevant document IDs.</param>
    /// <returns>MRR value between 0.0 and 1.0.</returns>
    public static double CalculateMRR(
        List<RetrievalResult> results,
        HashSet<string> relevantDocIds)
    {
        if (relevantDocIds.Count == 0 || results.Count == 0)
        {
            return 0.0;
        }

        for (int i = 0; i < results.Count; i++)
        {
            if (relevantDocIds.Contains(results[i].DocumentId.ToString()))
            {
                return 1.0 / (i + 1); // Reciprocal rank (1-indexed)
            }
        }

        return 0.0; // No relevant document found
    }

    /// <summary>
    /// Calculates percentile from a list of values.
    /// </summary>
    /// <param name="values">List of values.</param>
    /// <param name="percentile">Percentile to calculate (e.g., 0.95 for p95).</param>
    /// <returns>The percentile value.</returns>
    public static double CalculatePercentile(List<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        if (percentile < 0.0 || percentile > 1.0)
        {
            throw new ArgumentException("Percentile must be between 0.0 and 1.0.", nameof(percentile));
        }

        var sorted = values.OrderBy(v => v).ToList();
        var index = (int)Math.Ceiling(sorted.Count * percentile) - 1;
        index = Math.Max(0, Math.Min(index, sorted.Count - 1));

        return sorted[index];
    }
}
