namespace RAG.Evaluation.Models;

/// <summary>
/// Represents the result of a single metric evaluation.
/// </summary>
/// <param name="MetricName">The name of the metric that produced this result.</param>
/// <param name="Value">The calculated metric value.</param>
/// <param name="Timestamp">When the evaluation was performed.</param>
/// <param name="Configuration">Configuration parameters used for this evaluation.</param>
public record EvaluationResult(
    string MetricName,
    double Value,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, object> Configuration)
{
    /// <summary>
    /// Additional metadata about the evaluation (e.g., sample count, duration).
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } =
        new Dictionary<string, object>();

    /// <summary>
    /// Indicates whether the metric calculation succeeded.
    /// </summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>
    /// Error message if the calculation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a failed evaluation result.
    /// </summary>
    public static EvaluationResult Failure(string metricName, string errorMessage) =>
        new(metricName, double.NaN, DateTimeOffset.UtcNow, new Dictionary<string, object>())
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
}
