namespace RAG.Core.Domain;

/// <summary>
/// Represents a chunk of text extracted from a document.
/// </summary>
public class DocumentChunk
{
    /// <summary>
    /// Unique identifier for the chunk.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Reference to the parent document.
    /// </summary>
    public Guid DocumentId { get; init; }

    /// <summary>
    /// Text content of the chunk.
    /// </summary>
    public string Text { get; init; }

    /// <summary>
    /// Character position where chunk starts in original document (0-based).
    /// </summary>
    public int StartIndex { get; init; }

    /// <summary>
    /// Character position where chunk ends in original document (exclusive).
    /// </summary>
    public int EndIndex { get; init; }

    /// <summary>
    /// Sequential index of chunk within document (0-based).
    /// </summary>
    public int ChunkIndex { get; init; }

    /// <summary>
    /// Additional metadata about the chunk (e.g., token count, chunking strategy).
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; }

    /// <summary>
    /// Tenant ID for multi-tenancy isolation.
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Creates a new DocumentChunk instance with validation.
    /// </summary>
    public DocumentChunk(
        Guid id,
        Guid documentId,
        string text,
        int startIndex,
        int endIndex,
        int chunkIndex,
        Guid tenantId,
        Dictionary<string, object>? metadata = null)
    {
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        if (documentId == Guid.Empty)
            throw new ArgumentException("DocumentId cannot be empty", nameof(documentId));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty", nameof(tenantId));
        if (startIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(startIndex), "StartIndex must be >= 0");
        if (endIndex <= startIndex)
            throw new ArgumentOutOfRangeException(nameof(endIndex), "EndIndex must be > StartIndex");

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        DocumentId = documentId;
        Text = text;
        StartIndex = startIndex;
        EndIndex = endIndex;
        ChunkIndex = chunkIndex;
        TenantId = tenantId;
        Metadata = metadata ?? new Dictionary<string, object>();
    }
}
