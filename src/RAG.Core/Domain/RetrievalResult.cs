namespace RAG.Core.Domain;

/// <summary>
/// Represents a document retrieval result with relevance score.
/// </summary>
/// <param name="DocumentId">The ID of the retrieved document.</param>
/// <param name="Score">The relevance score from the retrieval algorithm (BM25 or dense).</param>
/// <param name="Text">The retrieved text snippet or chunk.</param>
/// <param name="Source">The source of the document (filename or path).</param>
/// <param name="HighlightedText">Optional highlighted snippet with matched terms emphasized.</param>
/// <param name="Metadata">Optional metadata for hybrid retrieval (e.g., individual BM25/Dense scores).</param>
public record RetrievalResult(
    Guid DocumentId,
    double Score,
    string Text,
    string Source,
    string? HighlightedText = null,
    Dictionary<string, object>? Metadata = null);
