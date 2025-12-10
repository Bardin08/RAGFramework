namespace RAG.Evaluation.Models;

/// <summary>
/// Aggregated results from running all metrics on a dataset.
/// </summary>
public class EvaluationReport
{
    /// <summary>
    /// Unique identifier for this evaluation run.
    /// </summary>
    public Guid RunId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// When the evaluation started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// When the evaluation completed.
    /// </summary>
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// Total duration of the evaluation.
    /// </summary>
    public TimeSpan Duration => CompletedAt - StartedAt;

    /// <summary>
    /// Number of samples evaluated.
    /// </summary>
    public int SampleCount { get; init; }

    /// <summary>
    /// Individual metric results for each sample.
    /// </summary>
    public IReadOnlyList<EvaluationResult> Results { get; init; } = [];

    /// <summary>
    /// Aggregated statistics per metric (mean, std, min, max).
    /// </summary>
    public IReadOnlyDictionary<string, MetricStatistics> Statistics { get; init; } =
        new Dictionary<string, MetricStatistics>();

    /// <summary>
    /// Configuration used for this evaluation run.
    /// </summary>
    public IReadOnlyDictionary<string, object> Configuration { get; init; } =
        new Dictionary<string, object>();
}

/// <summary>
/// Statistical summary for a single metric across all samples.
/// </summary>
public record MetricStatistics(
    string MetricName,
    double Mean,
    double StandardDeviation,
    double Min,
    double Max,
    int SuccessCount,
    int FailureCount);
