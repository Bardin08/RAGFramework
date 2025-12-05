using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Metrics.Retrieval;

/// <summary>
/// Computes Recall@K metric: proportion of relevant documents that are retrieved.
/// Formula: R@K = |{relevant documents in top K}| / |{all relevant documents}|
/// </summary>
public class RecallAtKMetric : IEvaluationMetric
{
    private readonly int _k;

    public RecallAtKMetric(int k = 10)
    {
        if (k <= 0) throw new ArgumentException("K must be positive", nameof(k));
        _k = k;
    }

    public string Name => $"Recall@{_k}";
    public string Description => $"Proportion of relevant documents found in top-{_k} results";

    public Task<double> CalculateAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        var relevantSet = context.RelevantDocumentIds.ToHashSet();

        // No relevant documents = undefined recall, return 0
        if (relevantSet.Count == 0)
            return Task.FromResult(0.0);

        var topK = context.RetrievedDocuments.Take(_k).ToList();
        if (topK.Count == 0)
            return Task.FromResult(0.0);

        var relevantInTopK = topK.Count(doc => relevantSet.Contains(doc.DocumentId));
        var recall = (double)relevantInTopK / relevantSet.Count;

        return Task.FromResult(recall);
    }
}
