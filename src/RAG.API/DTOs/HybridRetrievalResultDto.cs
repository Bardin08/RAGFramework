namespace RAG.API.DTOs;

/// <summary>
/// DTO representing a single retrieved document chunk from hybrid retrieval.
/// </summary>
/// <param name="DocumentId">Unique identifier of the document chunk.</param>
/// <param name="Score">Combined relevance score for this result (final score after reranking).</param>
/// <param name="Text">The content text of the chunk.</param>
/// <param name="Source">Source reference (e.g., filename, URL).</param>
/// <param name="HighlightedText">Highlighted version of the text with matching terms emphasized (from BM25).</param>
/// <param name="BM25Score">Individual normalized BM25 score [0,1] if chunk appeared in BM25 results, null otherwise.</param>
/// <param name="DenseScore">Individual Dense score [0,1] if chunk appeared in Dense results, null otherwise.</param>
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
