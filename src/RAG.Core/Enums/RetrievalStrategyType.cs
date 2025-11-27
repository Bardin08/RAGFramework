namespace RAG.Core.Enums;

/// <summary>
/// Defines the types of retrieval strategies available in the RAG system.
/// </summary>
public enum RetrievalStrategyType
{
    /// <summary>
    /// BM25 keyword-based retrieval using Elasticsearch.
    /// Best for exact keyword matches and term-based search.
    /// </summary>
    BM25 = 1,

    /// <summary>
    /// Dense semantic retrieval using vector embeddings and Qdrant.
    /// Best for semantic similarity and understanding query intent.
    /// </summary>
    Dense = 2,

    /// <summary>
    /// Hybrid retrieval combining BM25 and Dense strategies with reranking.
    /// Provides balanced results using both keyword and semantic matching.
    /// Reserved for future implementation (Epic 4).
    /// </summary>
    Hybrid = 3
}
