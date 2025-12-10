namespace RAG.Core.DTOs.Benchmark;

/// <summary>
/// Response containing benchmark job status information.
/// </summary>
public class BenchmarkJobResponse
{
    /// <summary>
    /// Unique identifier for the benchmark job.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Current status of the job (Created, Queued, Running, Completed, Failed).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Name of the dataset being benchmarked.
    /// </summary>
    public string Dataset { get; set; } = string.Empty;

    /// <summary>
    /// Configuration used for this benchmark.
    /// </summary>
    public BenchmarkConfiguration? Configuration { get; set; }

    /// <summary>
    /// Total number of samples in the benchmark.
    /// </summary>
    public int? TotalSamples { get; set; }

    /// <summary>
    /// Number of samples processed so far.
    /// </summary>
    public int? ProcessedSamples { get; set; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// When the job was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When the job started execution.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// When the job completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Error message if the job failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// User who initiated the job.
    /// </summary>
    public string? InitiatedBy { get; set; }

    /// <summary>
    /// Estimated duration to completion (if running).
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }
}
