namespace RAG.API.DTOs;

/// <summary>
/// DTO representing a single retrieved document chunk.
/// </summary>
/// <param name="DocumentId">Unique identifier of the document.</param>
/// <param name="Score">Relevance score for this result.</param>
/// <param name="Text">The content text of the chunk.</param>
/// <param name="Source">Source reference (e.g., filename, URL).</param>
/// <param name="HighlightedText">Highlighted version of the text with matching terms emphasized (BM25 specific).</param>
public record RetrievalResultDto(
    Guid DocumentId,
    double Score,
    string Text,
    string Source,
    string? HighlightedText
);
