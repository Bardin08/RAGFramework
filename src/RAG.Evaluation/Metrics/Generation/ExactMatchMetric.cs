using RAG.Evaluation.Interfaces;
using RAG.Evaluation.Models;

namespace RAG.Evaluation.Metrics.Generation;

/// <summary>
/// Computes Exact Match metric: checks if generated answer exactly matches expected answer.
/// Returns 1.0 for match, 0.0 for no match.
/// </summary>
public class ExactMatchMetric : IEvaluationMetric
{
    private readonly bool _normalize;

    /// <summary>
    /// Creates an Exact Match metric.
    /// </summary>
    /// <param name="normalize">Whether to normalize text before comparison (lowercase, remove punctuation).</param>
    public ExactMatchMetric(bool normalize = true)
    {
        _normalize = normalize;
    }

    public string Name => "ExactMatch";
    public string Description => "Binary metric: 1.0 if normalized answer matches ground truth exactly, 0.0 otherwise";

    public Task<double> CalculateAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.GroundTruth))
            return Task.FromResult(0.0);

        var generated = context.Response;
        var expected = context.GroundTruth;

        if (_normalize)
        {
            generated = TextNormalizer.Normalize(generated);
            expected = TextNormalizer.Normalize(expected);
        }

        var isMatch = string.Equals(generated, expected, StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(isMatch ? 1.0 : 0.0);
    }
}
