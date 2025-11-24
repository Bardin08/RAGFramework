namespace RAG.API.Models.Responses;

/// <summary>
/// Response model for document list item.
/// </summary>
public class DocumentListItemResponse
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
}
