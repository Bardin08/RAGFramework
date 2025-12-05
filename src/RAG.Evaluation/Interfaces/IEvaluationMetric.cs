using RAG.Evaluation.Models;

namespace RAG.Evaluation.Interfaces;

/// <summary>
/// Interface for evaluation metrics that assess RAG system performance.
/// Implementations measure specific aspects like retrieval quality, answer accuracy, etc.
/// </summary>
public interface IEvaluationMetric
{
    /// <summary>
    /// Gets the unique name of this metric (e.g., "Precision@K", "NDCG", "AnswerRelevance").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a brief description of what this metric measures.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Calculates the metric value for the given evaluation context.
    /// </summary>
    /// <param name="context">The evaluation context containing query, response, ground truth, and retrieved documents.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A value typically in range [0, 1] where higher is better, or domain-specific range.</returns>
    Task<double> CalculateAsync(EvaluationContext context, CancellationToken cancellationToken = default);
}
