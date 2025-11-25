using RAG.Core.Domain;

namespace RAG.Application.Interfaces;

/// <summary>
/// Interface for document retrieval strategies (BM25, Dense, Hybrid).
/// </summary>
public interface IRetriever
{
    /// <summary>
    /// Searches for relevant documents based on the query.
    /// </summary>
    /// <param name="query">The search query text.</param>
    /// <param name="topK">The number of top results to return.</param>
    /// <param name="tenantId">The tenant ID to filter results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of retrieval results ordered by relevance score (descending).</returns>
    Task<List<RetrievalResult>> SearchAsync(
        string query,
        int topK,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
