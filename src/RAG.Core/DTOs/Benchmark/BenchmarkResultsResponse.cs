namespace RAG.Core.DTOs.Benchmark;

/// <summary>
/// Response containing full benchmark results and metrics breakdown.
/// </summary>
public class BenchmarkResultsResponse
{
    /// <summary>
    /// Unique identifier for the benchmark job.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Name of the dataset that was benchmarked.
    /// </summary>
    public string Dataset { get; set; } = string.Empty;

    /// <summary>
    /// Configuration used for this benchmark.
    /// </summary>
    public BenchmarkConfiguration? Configuration { get; set; }

    /// <summary>
    /// When the benchmark was started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// When the benchmark completed.
    /// </summary>
    public DateTimeOffset CompletedAt { get; set; }

    /// <summary>
    /// Total duration of the benchmark.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Total number of samples evaluated.
    /// </summary>
    public int TotalSamples { get; set; }

    /// <summary>
    /// Number of successful evaluations.
    /// </summary>
    public int SuccessfulSamples { get; set; }

    /// <summary>
    /// Number of failed evaluations.
    /// </summary>
    public int FailedSamples { get; set; }

    /// <summary>
    /// Aggregated metrics with summary statistics.
    /// Key: Metric name (e.g., "Precision", "Recall", "F1Score")
    /// Value: Metric statistics (mean, std dev, min, max, etc.)
    /// </summary>
    public Dictionary<string, MetricSummary> Metrics { get; set; } = new();

    /// <summary>
    /// Optional per-sample detailed results.
    /// Only included if requested explicitly.
    /// </summary>
    public List<SampleResult>? DetailedResults { get; set; }

    /// <summary>
    /// User who initiated the benchmark.
    /// </summary>
    public string? InitiatedBy { get; set; }
}

/// <summary>
/// Summary statistics for a single metric.
/// </summary>
public class MetricSummary
{
    /// <summary>
    /// Name of the metric.
    /// </summary>
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// Mean (average) value across all samples.
    /// </summary>
    public double Mean { get; set; }

    /// <summary>
    /// Standard deviation.
    /// </summary>
    public double StandardDeviation { get; set; }

    /// <summary>
    /// Minimum value observed.
    /// </summary>
    public double Min { get; set; }

    /// <summary>
    /// Maximum value observed.
    /// </summary>
    public double Max { get; set; }

    /// <summary>
    /// Median value.
    /// </summary>
    public double? Median { get; set; }

    /// <summary>
    /// Number of successful measurements.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed measurements.
    /// </summary>
    public int FailureCount { get; set; }
}

/// <summary>
/// Result for a single sample in the benchmark.
/// </summary>
public class SampleResult
{
    /// <summary>
    /// Identifier of the sample.
    /// </summary>
    public string SampleId { get; set; } = string.Empty;

    /// <summary>
    /// Query text.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Whether the evaluation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Metric values for this sample.
    /// Key: Metric name
    /// Value: Metric value
    /// </summary>
    public Dictionary<string, double> Metrics { get; set; } = new();

    /// <summary>
    /// Error message if evaluation failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Duration of evaluation for this sample.
    /// </summary>
    public TimeSpan Duration { get; set; }
}
