using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RAG.Core.Domain;

/// <summary>
/// Represents a benchmark job for tracking evaluation runs.
/// Stored in database for persistent tracking across application restarts.
/// </summary>
[Table("benchmark_jobs")]
public class BenchmarkJob
{
    /// <summary>
    /// Unique identifier for the job.
    /// </summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Current status: Created, Queued, Running, Completed, Failed.
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "Created";

    /// <summary>
    /// Name of the dataset being benchmarked.
    /// </summary>
    [Required]
    [MaxLength(200)]
    [Column("dataset")]
    public string Dataset { get; set; } = string.Empty;

    /// <summary>
    /// Configuration for the benchmark (stored as JSON).
    /// </summary>
    [Required]
    [Column("configuration")]
    public string Configuration { get; set; } = "{}";

    /// <summary>
    /// Optional limit on number of samples.
    /// </summary>
    [Column("sample_size")]
    public int? SampleSize { get; set; }

    /// <summary>
    /// Results of the benchmark (stored as JSON).
    /// </summary>
    [Column("results")]
    public string? Results { get; set; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    [Column("progress")]
    public int Progress { get; set; }

    /// <summary>
    /// Total number of samples in the benchmark.
    /// </summary>
    [Column("total_samples")]
    public int? TotalSamples { get; set; }

    /// <summary>
    /// Number of samples processed so far.
    /// </summary>
    [Column("processed_samples")]
    public int? ProcessedSamples { get; set; }

    /// <summary>
    /// Error message if the job failed.
    /// </summary>
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When the job was created.
    /// </summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the job started execution.
    /// </summary>
    [Column("started_at")]
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// When the job completed.
    /// </summary>
    [Column("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// User who initiated the job.
    /// </summary>
    [Column("initiated_by")]
    public string? InitiatedBy { get; set; }

    /// <summary>
    /// Calculates the duration of the job.
    /// </summary>
    [NotMapped]
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;
}

/// <summary>
/// Status of a benchmark job.
/// </summary>
public enum BenchmarkJobStatus
{
    /// <summary>
    /// Job has been created but not yet queued.
    /// </summary>
    Created,

    /// <summary>
    /// Job is queued for execution.
    /// </summary>
    Queued,

    /// <summary>
    /// Job is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// Job completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Job failed with an error.
    /// </summary>
    Failed
}
