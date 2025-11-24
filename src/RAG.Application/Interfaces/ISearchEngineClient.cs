using RAG.Core.Domain;

namespace RAG.Application.Interfaces;

/// <summary>
/// Defines a client for interacting with a search engine (e.g., Elasticsearch).
/// </summary>
public interface ISearchEngineClient
{
    /// <summary>
    /// Initializes the search index with the appropriate mapping.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitializeIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes a single document chunk in the search engine.
    /// </summary>
    /// <param name="chunk">The document chunk to index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IndexDocumentAsync(DocumentChunk chunk, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk indexes multiple document chunks in the search engine.
    /// </summary>
    /// <param name="chunks">The document chunks to index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BulkIndexAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a BM25 full-text search on the indexed documents.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="topK">The number of top results to return.</param>
    /// <param name="tenantId">The tenant ID to filter results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching document chunks with their relevance scores.</returns>
    Task<List<(DocumentChunk Chunk, double Score)>> SearchAsync(
        string query, 
        int topK, 
        Guid tenantId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document chunk from the search engine.
    /// </summary>
    /// <param name="chunkId">The ID of the chunk to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteDocumentAsync(Guid chunkId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all chunks associated with a document.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteDocumentChunksAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken = default);
}
