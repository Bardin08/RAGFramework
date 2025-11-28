namespace RAG.API.DTOs;

/// <summary>
/// Response DTO for retrieval operations.
/// </summary>
/// <param name="Results">List of retrieved document chunks, ordered by relevance.</param>
/// <param name="TotalFound">Total number of matching documents found.</param>
/// <param name="RetrievalTimeMs">Time taken for the retrieval operation in milliseconds.</param>
/// <param name="Strategy">Name of the retrieval strategy used (e.g., "BM25", "Dense", "Hybrid").</param>
public record RetrievalResponse(
    List<RetrievalResultDto> Results,
    int TotalFound,
    double RetrievalTimeMs,
    string Strategy
);
