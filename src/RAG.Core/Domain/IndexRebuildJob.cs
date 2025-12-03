namespace RAG.Core.Domain;

/// <summary>
/// Represents an index rebuild job for tracking purposes.
/// </summary>
public class IndexRebuildJob
{
    /// <summary>
    /// Unique identifier for the job.
    /// </summary>
    public Guid JobId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Optional tenant ID to rebuild index for.
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>
    /// Whether to regenerate embeddings.
    /// </summary>
    public bool IncludeEmbeddings { get; init; } = true;

    /// <summary>
    /// Current status: Queued, InProgress, Completed, Failed.
    /// </summary>
    public string Status { get; set; } = "Queued";

    /// <summary>
    /// Estimated number of documents to process.
    /// </summary>
    public int EstimatedDocuments { get; set; }

    /// <summary>
    /// Number of documents processed so far.
    /// </summary>
    public int ProcessedDocuments { get; set; }

    /// <summary>
    /// When the job was created/queued.
    /// </summary>
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the job completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Cancellation token source for the job.
    /// </summary>
    public CancellationTokenSource? CancellationTokenSource { get; set; }
}
