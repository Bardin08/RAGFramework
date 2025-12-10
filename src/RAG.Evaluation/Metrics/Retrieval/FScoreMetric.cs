using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Metrics.Retrieval;

/// <summary>
/// Computes F-Score metric: harmonic mean of precision and recall.
/// Formula: F_beta = (1 + beta^2) * (precision * recall) / (beta^2 * precision + recall)
/// For F1 (beta=1): F1 = 2 * (precision * recall) / (precision + recall)
/// </summary>
public class FScoreMetric : IEvaluationMetric
{
    private readonly int _k;
    private readonly double _beta;

    /// <summary>
    /// Creates an F-Score metric.
    /// </summary>
    /// <param name="k">Number of top results to consider.</param>
    /// <param name="beta">Beta parameter. Beta=1 gives F1, Beta&lt;1 favors precision, Beta&gt;1 favors recall.</param>
    public FScoreMetric(int k = 10, double beta = 1.0)
    {
        if (k <= 0) throw new ArgumentException("K must be positive", nameof(k));
        if (beta <= 0) throw new ArgumentException("Beta must be positive", nameof(beta));
        _k = k;
        _beta = beta;
    }

    public string Name => _beta == 1.0 ? $"F1@{_k}" : $"F{_beta:F1}@{_k}";
    public string Description => $"F-Score (beta={_beta}) at top-{_k} results";

    public Task<double> CalculateAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        var relevantSet = context.RelevantDocumentIds.ToHashSet();

        if (relevantSet.Count == 0)
            return Task.FromResult(0.0);

        var topK = context.RetrievedDocuments.Take(_k).ToList();
        if (topK.Count == 0)
            return Task.FromResult(0.0);

        var relevantInTopK = topK.Count(doc => relevantSet.Contains(doc.DocumentId));

        // Calculate precision and recall
        var precision = (double)relevantInTopK / topK.Count;
        var recall = (double)relevantInTopK / relevantSet.Count;

        // Handle zero cases
        if (precision + recall == 0)
            return Task.FromResult(0.0);

        // F-beta score formula
        var betaSquared = _beta * _beta;
        var fScore = (1 + betaSquared) * (precision * recall) / (betaSquared * precision + recall);

        return Task.FromResult(fScore);
    }
}
