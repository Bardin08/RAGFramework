namespace RAG.Core.Domain;

/// <summary>
/// Represents the result of text extraction from a document.
/// </summary>
public record TextExtractionResult
{
    /// <summary>
    /// Gets the extracted text content from the document.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Gets the metadata extracted from the document (e.g., Author, Title, CreationDate).
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}
