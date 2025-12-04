using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RAG.Core.Domain;

/// <summary>
/// Represents an index rebuild job for tracking purposes.
/// Stored in database for persistent tracking across application restarts.
/// </summary>
[Table("index_rebuild_jobs")]
public class IndexRebuildJob
{
    /// <summary>
    /// Unique identifier for the job.
    /// </summary>
    [Key]
    [Column("id")]
    public Guid JobId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Optional tenant ID to rebuild index for.
    /// </summary>
    [Column("tenant_id")]
    public Guid? TenantId { get; init; }

    /// <summary>
    /// Whether to regenerate embeddings.
    /// </summary>
    [Column("include_embeddings")]
    public bool IncludeEmbeddings { get; init; } = true;

    /// <summary>
    /// Current status: Queued, InProgress, Completed, Failed.
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "Queued";

    /// <summary>
    /// Estimated number of documents to process.
    /// </summary>
    [Column("estimated_documents")]
    public int EstimatedDocuments { get; set; }

    /// <summary>
    /// Number of documents processed so far.
    /// </summary>
    [Column("processed_documents")]
    public int ProcessedDocuments { get; set; }

    /// <summary>
    /// When the job was created/queued.
    /// </summary>
    [Column("started_at")]
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the job completed.
    /// </summary>
    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    [Column("error")]
    public string? Error { get; set; }

    /// <summary>
    /// User who initiated the job.
    /// </summary>
    [Column("initiated_by")]
    public Guid? InitiatedBy { get; set; }

    /// <summary>
    /// Cancellation token source for the job (not persisted).
    /// </summary>
    [NotMapped]
    public CancellationTokenSource? CancellationTokenSource { get; set; }
}
