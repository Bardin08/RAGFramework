using RAG.Core.Domain;

namespace RAG.Application.Reranking;

/// <summary>
/// Interface for Reciprocal Rank Fusion (RRF) reranking algorithm.
/// Combines ranked lists from multiple retrieval sources to produce a unified ranking.
/// </summary>
public interface IRRFReranker
{
    /// <summary>
    /// Reranks document results from multiple retrieval sources using Reciprocal Rank Fusion.
    /// </summary>
    /// <param name="resultSets">
    /// List of ranked result sets from different retrieval methods (e.g., BM25, Dense).
    /// Each inner list represents results from one retrieval source, ordered by relevance.
    /// </param>
    /// <param name="topK">
    /// Number of top-ranked documents to return in the final reranked list.
    /// </param>
    /// <returns>
    /// Reranked list of documents sorted by RRF score (descending), limited to top-K results.
    /// Documents appearing in multiple result sets receive combined RRF scores.
    /// </returns>
    /// <remarks>
    /// RRF Formula: RRF(d) = Σ 1 / (k + rank(d)) where k is a constant (typically 60).
    /// Documents are deduplicated by DocumentId, with RRF scores summed across all sources.
    /// </remarks>
    List<RetrievalResult> Rerank(List<List<RetrievalResult>> resultSets, int topK);
}
