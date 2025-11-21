namespace RAG.Core.Domain;

/// <summary>
/// Represents a document in the RAG system.
/// </summary>
public class Document
{
    /// <summary>
    /// Unique identifier for the document.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The title of the document.
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// The full content of the document.
    /// </summary>
    public string Content { get; init; }

    /// <summary>
    /// The original source or URL of the document.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Flexible metadata associated with the document.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; }

    /// <summary>
    /// List of chunk IDs that belong to this document.
    /// </summary>
    public List<Guid> ChunkIds { get; init; }

    /// <summary>
    /// Creates a new Document instance with validation.
    /// </summary>
    public Document(Guid id, string title, string content, string? source = null,
        Dictionary<string, object>? metadata = null, List<Guid>? chunkIds = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Document ID cannot be empty", nameof(id));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Document title cannot be empty", nameof(title));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Document content cannot be empty", nameof(content));

        Id = id;
        Title = title;
        Content = content;
        Source = source;
        Metadata = metadata ?? new Dictionary<string, object>();
        ChunkIds = chunkIds ?? new List<Guid>();
    }
}
