using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Metrics.Retrieval;

/// <summary>
/// Computes Mean Reciprocal Rank (MRR) metric: inverse of the rank of the first relevant document.
/// Formula: MRR = 1 / rank_of_first_relevant
/// </summary>
public class MeanReciprocalRankMetric : IEvaluationMetric
{
    public string Name => "MRR";
    public string Description => "Reciprocal of the rank at which the first relevant document appears";

    public Task<double> CalculateAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        var relevantSet = context.RelevantDocumentIds.ToHashSet();

        if (relevantSet.Count == 0)
            return Task.FromResult(0.0);

        if (context.RetrievedDocuments.Count == 0)
            return Task.FromResult(0.0);

        // Find rank of first relevant document (1-indexed)
        for (var i = 0; i < context.RetrievedDocuments.Count; i++)
        {
            if (relevantSet.Contains(context.RetrievedDocuments[i].DocumentId))
            {
                var rank = i + 1;
                return Task.FromResult(1.0 / rank);
            }
        }

        // No relevant document found
        return Task.FromResult(0.0);
    }
}
