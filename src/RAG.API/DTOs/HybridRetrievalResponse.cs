namespace RAG.API.DTOs;

/// <summary>
/// Response DTO for hybrid retrieval operations.
/// Contains results from combined BM25 and Dense retrieval with detailed metadata.
/// </summary>
/// <param name="Results">List of retrieved document chunks, ordered by combined relevance score.</param>
/// <param name="TotalFound">Total number of matching documents found.</param>
/// <param name="RetrievalTimeMs">Time taken for the retrieval operation in milliseconds.</param>
/// <param name="Strategy">Name of the retrieval strategy used (always "Hybrid").</param>
/// <param name="Metadata">Additional metadata about the retrieval process.</param>
public record HybridRetrievalResponse(
    List<HybridRetrievalResultDto> Results,
    int TotalFound,
    double RetrievalTimeMs,
    string Strategy,
    HybridRetrievalMetadata Metadata
);

/// <summary>
/// Metadata for hybrid retrieval response showing how retrievers contributed.
/// </summary>
/// <param name="Alpha">Weight used for BM25 results in combination.</param>
/// <param name="Beta">Weight used for Dense results in combination.</param>
/// <param name="RerankingMethod">Reranking method used ("Weighted" or "RRF").</param>
/// <param name="BM25ResultCount">Count of BM25 results BEFORE deduplication.</param>
/// <param name="DenseResultCount">Count of Dense results BEFORE deduplication.</param>
public record HybridRetrievalMetadata(
    double Alpha,
    double Beta,
    string RerankingMethod,
    int BM25ResultCount,
    int DenseResultCount
);
