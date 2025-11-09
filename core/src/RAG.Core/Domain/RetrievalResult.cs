namespace RAG.Core.Domain;

/// <summary>
/// Represents a document retrieval result with relevance score.
/// </summary>
/// <param name="DocumentId">The ID of the retrieved document.</param>
/// <param name="Score">The relevance score from the retrieval algorithm.</param>
/// <param name="Text">The retrieved text snippet.</param>
/// <param name="Source">The source of the document.</param>
public record RetrievalResult(
    Guid DocumentId,
    float Score,
    string Text,
    string Source);
