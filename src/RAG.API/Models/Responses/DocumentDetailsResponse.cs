namespace RAG.API.Models.Responses;

/// <summary>
/// Response model for detailed document information.
/// </summary>
public class DocumentDetailsResponse
{
    /// <summary>
    /// Unique identifier for the document.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Document title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Document source (filename or origin).
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Number of chunks the document was split into.
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// When the document was indexed.
    /// </summary>
    public DateTime IndexedAt { get; set; }

    /// <summary>
    /// Document chunks with preview text.
    /// </summary>
    public List<DocumentChunkInfo> Chunks { get; set; } = new();

    /// <summary>
    /// Additional document metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Information about a document chunk.
/// </summary>
public class DocumentChunkInfo
{
    /// <summary>
    /// Chunk unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Chunk index in the document.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Preview of the chunk text (truncated for display).
    /// </summary>
    public string TextPreview { get; set; } = string.Empty;

    /// <summary>
    /// Start position in the original document.
    /// </summary>
    public int StartIndex { get; set; }

    /// <summary>
    /// End position in the original document.
    /// </summary>
    public int EndIndex { get; set; }
}
