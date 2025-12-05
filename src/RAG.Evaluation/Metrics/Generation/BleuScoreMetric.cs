using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Metrics.Generation;

/// <summary>
/// Computes BLEU (Bilingual Evaluation Understudy) score.
/// Measures n-gram overlap between generated and reference text.
/// </summary>
public class BleuScoreMetric : IEvaluationMetric
{
    private readonly int _maxN;

    /// <summary>
    /// Creates a BLEU score metric.
    /// </summary>
    /// <param name="maxN">Maximum n-gram size (typically 4).</param>
    public BleuScoreMetric(int maxN = 4)
    {
        if (maxN is < 1 or > 4) throw new ArgumentException("maxN must be between 1 and 4", nameof(maxN));
        _maxN = maxN;
    }

    public string Name => $"BLEU-{_maxN}";
    public string Description => $"BLEU score using n-grams up to {_maxN}";

    public Task<double> CalculateAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.GroundTruth))
            return Task.FromResult(0.0);

        var candidateTokens = TextNormalizer.TokenizeNormalized(context.Response);
        var referenceTokens = TextNormalizer.TokenizeNormalized(context.GroundTruth);

        if (candidateTokens.Count == 0)
            return Task.FromResult(0.0);

        // Calculate modified precision for each n-gram
        var precisions = new List<double>();
        for (var n = 1; n <= _maxN; n++)
        {
            var precision = CalculateModifiedPrecision(candidateTokens, referenceTokens, n);
            if (precision == 0)
                return Task.FromResult(0.0); // Any zero precision = zero BLEU
            precisions.Add(precision);
        }

        // Geometric mean of precisions
        var geometricMean = Math.Exp(precisions.Sum(p => Math.Log(p)) / precisions.Count);

        // Brevity penalty
        var brevityPenalty = CalculateBrevityPenalty(candidateTokens.Count, referenceTokens.Count);

        var bleu = brevityPenalty * geometricMean;
        return Task.FromResult(bleu);
    }

    private static double CalculateModifiedPrecision(
        IReadOnlyList<string> candidate,
        IReadOnlyList<string> reference,
        int n)
    {
        var candidateNGrams = TextNormalizer.ExtractNGrams(candidate, n);
        var referenceNGrams = TextNormalizer.ExtractNGrams(reference, n);

        if (candidateNGrams.Count == 0)
            return 0.0;

        // Count reference n-grams
        var referenceCounts = new Dictionary<string, int>();
        foreach (var ngram in referenceNGrams)
        {
            referenceCounts.TryGetValue(ngram, out var count);
            referenceCounts[ngram] = count + 1;
        }

        // Count clipped matches
        var candidateCounts = new Dictionary<string, int>();
        foreach (var ngram in candidateNGrams)
        {
            candidateCounts.TryGetValue(ngram, out var count);
            candidateCounts[ngram] = count + 1;
        }

        var clippedCount = 0;
        foreach (var (ngram, count) in candidateCounts)
        {
            referenceCounts.TryGetValue(ngram, out var refCount);
            clippedCount += Math.Min(count, refCount);
        }

        return (double)clippedCount / candidateNGrams.Count;
    }

    private static double CalculateBrevityPenalty(int candidateLength, int referenceLength)
    {
        if (candidateLength >= referenceLength)
            return 1.0;

        return Math.Exp(1 - (double)referenceLength / candidateLength);
    }
}
