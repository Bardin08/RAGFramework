namespace RAG.API.DTOs;

/// <summary>
/// DTO representing a single retrieved document chunk from hybrid retrieval.
/// Includes individual BM25 and Dense scores along with the combined score.
/// </summary>
/// <param name="DocumentId">Unique identifier of the document.</param>
/// <param name="Score">Combined relevance score for this result (final score after reranking).</param>
/// <param name="Text">The content text of the chunk.</param>
/// <param name="Source">Source reference (e.g., filename, URL).</param>
/// <param name="HighlightedText">Highlighted version of the text with matching terms emphasized.</param>
/// <param name="BM25Score">Individual BM25 (keyword-based) relevance score. Null if document not found by BM25.</param>
/// <param name="DenseScore">Individual Dense (semantic) relevance score. Null if document not found by Dense retriever.</param>
/// <param name="CombinedScore">The final combined score after applying weighted scoring or RRF.</param>
public record HybridRetrievalResultDto(
    Guid DocumentId,
    double Score,
    string Text,
    string Source,
    string? HighlightedText,
    double? BM25Score,
    double? DenseScore,
    double CombinedScore
);
