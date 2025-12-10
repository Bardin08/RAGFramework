namespace RAG.Evaluation.Configuration;

/// <summary>
/// Configuration options for evaluation runs.
/// </summary>
public class EvaluationOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Evaluation";

    /// <summary>
    /// Path to the evaluation dataset file.
    /// </summary>
    public string? DatasetPath { get; set; }

    /// <summary>
    /// Path to ground truth data file.
    /// </summary>
    public string? GroundTruthPath { get; set; }

    /// <summary>
    /// List of metric names to run. If empty, all registered metrics are run.
    /// </summary>
    public List<string> MetricsToRun { get; set; } = [];

    /// <summary>
    /// Maximum degree of parallelism for metric calculations.
    /// </summary>
    public int MaxParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Directory to output evaluation reports.
    /// </summary>
    public string OutputDirectory { get; set; } = "./evaluation-results";

    /// <summary>
    /// Whether to save detailed per-sample results.
    /// </summary>
    public bool SaveDetailedResults { get; set; } = true;

    /// <summary>
    /// Timeout for individual metric calculations in seconds.
    /// </summary>
    public int MetricTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Cache duration for ground truth data in minutes.
    /// </summary>
    public int GroundTruthCacheMinutes { get; set; } = 5;

    /// <summary>
    /// K values to use for Precision@K and Recall@K metrics.
    /// </summary>
    public List<int> KValues { get; set; } = [5, 10, 20];

    /// <summary>
    /// Data retention: number of days to keep evaluation runs.
    /// </summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    /// Data retention: maximum number of runs to keep per evaluation.
    /// </summary>
    public int? MaxRunsPerEvaluation { get; set; }
}
