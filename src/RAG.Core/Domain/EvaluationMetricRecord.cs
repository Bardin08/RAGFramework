namespace RAG.Core.Domain;

/// <summary>
/// Stores individual metric values from evaluation runs.
/// </summary>
public class EvaluationMetricRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Reference to the evaluation run.
    /// </summary>
    public Guid RunId { get; set; }

    /// <summary>
    /// Name of the metric (e.g., "Precision@10", "MRR", "TokenF1").
    /// </summary>
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// The calculated metric value.
    /// </summary>
    public decimal MetricValue { get; set; }

    /// <summary>
    /// Additional metadata about this metric record (JSONB).
    /// Per-query breakdown, configuration parameters, etc.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// When this metric was recorded.
    /// </summary>
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional sample/query ID this metric was calculated for.
    /// </summary>
    public string? SampleId { get; set; }

    /// <summary>
    /// Navigation property to the run.
    /// </summary>
    public EvaluationRun? Run { get; set; }
}
