namespace RAG.Core.DTOs.Admin;

/// <summary>
/// Response for index rebuild operation.
/// </summary>
public class IndexRebuildResponse
{
    /// <summary>
    /// Unique identifier for the rebuild job.
    /// </summary>
    public Guid JobId { get; init; }

    /// <summary>
    /// Current status of the job: Queued, InProgress, Completed, Failed.
    /// </summary>
    public string Status { get; init; } = "Queued";

    /// <summary>
    /// Estimated number of documents to be reindexed.
    /// </summary>
    public int EstimatedDocuments { get; init; }

    /// <summary>
    /// Number of documents processed so far.
    /// </summary>
    public int ProcessedDocuments { get; init; }

    /// <summary>
    /// Timestamp when the job was started.
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// Timestamp when the job completed (if applicable).
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Error message if the job failed.
    /// </summary>
    public string? Error { get; init; }
}
