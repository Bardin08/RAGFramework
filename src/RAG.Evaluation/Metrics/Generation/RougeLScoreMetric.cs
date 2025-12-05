using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Metrics.Generation;

/// <summary>
/// Computes ROUGE-L (Longest Common Subsequence) score.
/// Measures the longest common subsequence between generated and reference text.
/// </summary>
public class RougeLScoreMetric : IEvaluationMetric
{
    private readonly double _beta;

    /// <summary>
    /// Creates a ROUGE-L metric.
    /// </summary>
    /// <param name="beta">Weight for recall vs precision. Default 1.0 gives equal weight (F1).</param>
    public RougeLScoreMetric(double beta = 1.0)
    {
        if (beta <= 0) throw new ArgumentException("Beta must be positive", nameof(beta));
        _beta = beta;
    }

    public string Name => "ROUGE-L";
    public string Description => "ROUGE-L score based on longest common subsequence";

    public Task<double> CalculateAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.GroundTruth))
            return Task.FromResult(0.0);

        var candidateTokens = TextNormalizer.TokenizeNormalized(context.Response);
        var referenceTokens = TextNormalizer.TokenizeNormalized(context.GroundTruth);

        if (candidateTokens.Count == 0 && referenceTokens.Count == 0)
            return Task.FromResult(1.0);

        if (candidateTokens.Count == 0 || referenceTokens.Count == 0)
            return Task.FromResult(0.0);

        var lcsLength = TextNormalizer.LongestCommonSubsequence(candidateTokens, referenceTokens);

        if (lcsLength == 0)
            return Task.FromResult(0.0);

        // Recall: LCS / reference length
        var recall = (double)lcsLength / referenceTokens.Count;

        // Precision: LCS / candidate length
        var precision = (double)lcsLength / candidateTokens.Count;

        // F-measure with beta
        var betaSquared = _beta * _beta;
        var fScore = (1 + betaSquared) * precision * recall / (betaSquared * precision + recall);

        return Task.FromResult(fScore);
    }
}
