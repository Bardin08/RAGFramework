namespace RAG.Core.Domain;

/// <summary>
/// Represents a chunk of a document with its embedding vector.
/// </summary>
public record DocumentChunk
{
    /// <summary>
    /// Unique identifier for the chunk.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The ID of the parent document.
    /// </summary>
    public Guid DocumentId { get; init; }

    /// <summary>
    /// The text content of the chunk.
    /// </summary>
    public string Text { get; init; }

    /// <summary>
    /// The vector embedding of the chunk (typically 384 or 768 dimensions).
    /// </summary>
    public float[] Embedding { get; init; }

    /// <summary>
    /// The starting character position in the original document.
    /// </summary>
    public int StartIndex { get; init; }

    /// <summary>
    /// The ending character position in the original document.
    /// </summary>
    public int EndIndex { get; init; }

    /// <summary>
    /// Creates a new DocumentChunk instance with validation.
    /// </summary>
    public DocumentChunk(Guid id, Guid documentId, string text, float[] embedding, int startIndex, int endIndex)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Chunk ID cannot be empty", nameof(id));

        if (documentId == Guid.Empty)
            throw new ArgumentException("Document ID cannot be empty", nameof(documentId));

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Chunk text cannot be empty", nameof(text));

        if (embedding == null || embedding.Length == 0)
            throw new ArgumentException("Embedding cannot be null or empty", nameof(embedding));

        if (startIndex < 0)
            throw new ArgumentException("StartIndex cannot be negative", nameof(startIndex));

        if (endIndex <= startIndex)
            throw new ArgumentException("EndIndex must be greater than StartIndex", nameof(endIndex));

        Id = id;
        DocumentId = documentId;
        Text = text;
        Embedding = embedding;
        StartIndex = startIndex;
        EndIndex = endIndex;
    }
}
