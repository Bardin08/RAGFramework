namespace RAG.Core.Domain;

/// <summary>
/// Represents a single evaluation run with its status and results.
/// </summary>
public class EvaluationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Reference to the evaluation configuration (optional).
    /// </summary>
    public Guid? EvaluationId { get; set; }

    /// <summary>
    /// Name of this evaluation run.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description or notes about this run.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When the run was started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the run was finished (null if still running).
    /// </summary>
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>
    /// Current status of the run.
    /// </summary>
    public EvaluationRunStatus Status { get; set; } = EvaluationRunStatus.Queued;

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Snapshot of configuration at run time (JSONB).
    /// </summary>
    public string Configuration { get; set; } = "{}";

    /// <summary>
    /// Aggregated results summary (JSONB).
    /// </summary>
    public string? ResultsSummary { get; set; }

    /// <summary>
    /// Error message if the run failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Total number of queries in the evaluation.
    /// </summary>
    public int TotalQueries { get; set; }

    /// <summary>
    /// Number of queries successfully completed.
    /// </summary>
    public int CompletedQueries { get; set; }

    /// <summary>
    /// Number of queries that failed.
    /// </summary>
    public int FailedQueries { get; set; }

    /// <summary>
    /// User who initiated the run.
    /// </summary>
    public string? InitiatedBy { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy support.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Calculates the duration of the run.
    /// </summary>
    public TimeSpan? Duration => FinishedAt - StartedAt;
}

/// <summary>
/// Status of an evaluation run.
/// </summary>
public enum EvaluationRunStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}
