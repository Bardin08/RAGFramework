using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Metrics.Retrieval;

/// <summary>
/// Computes Precision@K metric: proportion of retrieved documents that are relevant.
/// Formula: P@K = |{relevant documents in top K}| / K
/// </summary>
public class PrecisionAtKMetric : IEvaluationMetric
{
    private readonly int _k;

    public PrecisionAtKMetric(int k = 10)
    {
        if (k <= 0) throw new ArgumentException("K must be positive", nameof(k));
        _k = k;
    }

    public string Name => $"Precision@{_k}";
    public string Description => $"Proportion of top-{_k} retrieved documents that are relevant";

    public Task<double> CalculateAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        if (context.RetrievedDocuments.Count == 0)
            return Task.FromResult(0.0);

        var relevantSet = context.RelevantDocumentIds.ToHashSet();
        if (relevantSet.Count == 0)
            return Task.FromResult(0.0);

        var topK = context.RetrievedDocuments.Take(_k).ToList();
        var effectiveK = Math.Min(_k, topK.Count);

        if (effectiveK == 0)
            return Task.FromResult(0.0);

        var relevantInTopK = topK.Count(doc => relevantSet.Contains(doc.DocumentId));
        var precision = (double)relevantInTopK / effectiveK;

        return Task.FromResult(precision);
    }
}
