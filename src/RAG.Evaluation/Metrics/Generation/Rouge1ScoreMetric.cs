using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Metrics.Generation;

/// <summary>
/// Computes ROUGE-1 (unigram) score.
/// Measures unigram overlap between generated and reference text.
/// </summary>
public class Rouge1ScoreMetric : IEvaluationMetric
{
    public string Name => "ROUGE-1";
    public string Description => "ROUGE-1 score based on unigram overlap";

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

        // Count unigrams
        var candidateCounts = CountUnigrams(candidateTokens);
        var referenceCounts = CountUnigrams(referenceTokens);

        // Calculate overlap
        var overlap = 0;
        foreach (var (token, count) in candidateCounts)
        {
            if (referenceCounts.TryGetValue(token, out var refCount))
                overlap += Math.Min(count, refCount);
        }

        if (overlap == 0)
            return Task.FromResult(0.0);

        var precision = (double)overlap / candidateTokens.Count;
        var recall = (double)overlap / referenceTokens.Count;

        var f1 = 2 * precision * recall / (precision + recall);
        return Task.FromResult(f1);
    }

    private static Dictionary<string, int> CountUnigrams(IReadOnlyList<string> tokens)
    {
        var counts = new Dictionary<string, int>();
        foreach (var token in tokens)
        {
            counts.TryGetValue(token, out var count);
            counts[token] = count + 1;
        }
        return counts;
    }
}
