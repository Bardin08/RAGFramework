using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Metrics.Generation;

/// <summary>
/// Computes token-level F1 score between generated and expected answers.
/// Measures overlap of tokens between the two texts.
/// </summary>
public class TokenF1Metric : IEvaluationMetric
{
    public string Name => "TokenF1";
    public string Description => "Token-level F1 score measuring overlap between generated and expected answers";

    public Task<double> CalculateAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.GroundTruth))
            return Task.FromResult(0.0);

        var generatedTokens = TextNormalizer.TokenizeNormalized(context.Response);
        var expectedTokens = TextNormalizer.TokenizeNormalized(context.GroundTruth);

        if (generatedTokens.Count == 0 && expectedTokens.Count == 0)
            return Task.FromResult(1.0); // Both empty = perfect match

        if (generatedTokens.Count == 0 || expectedTokens.Count == 0)
            return Task.FromResult(0.0);

        var generatedSet = generatedTokens.ToHashSet();
        var expectedSet = expectedTokens.ToHashSet();

        var overlap = generatedSet.Intersect(expectedSet).Count();

        if (overlap == 0)
            return Task.FromResult(0.0);

        var precision = (double)overlap / generatedSet.Count;
        var recall = (double)overlap / expectedSet.Count;

        var f1 = 2 * precision * recall / (precision + recall);
        return Task.FromResult(f1);
    }
}
