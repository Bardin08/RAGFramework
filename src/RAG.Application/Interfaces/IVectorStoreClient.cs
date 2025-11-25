using RAG.Core.Domain;

namespace RAG.Application.Interfaces;

/// <summary>
/// Defines a client for interacting with a vector store (e.g., Qdrant).
/// </summary>
public interface IVectorStoreClient
{
    /// <summary>
    /// Initializes the vector collection with the appropriate configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitializeCollectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a single vector with metadata.
    /// </summary>
    /// <param name="id">Unique identifier for the vector.</param>
    /// <param name="embedding">The embedding vector.</param>
    /// <param name="payload">Metadata payload including documentId, text, tenantId, etc.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertVectorAsync(
        Guid id, 
        float[] embedding, 
        Dictionary<string, object> payload, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch upserts multiple vectors with metadata.
    /// </summary>
    /// <param name="vectors">List of (id, embedding, payload) tuples.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BatchUpsertAsync(
        IEnumerable<(Guid Id, float[] Embedding, Dictionary<string, object> Payload)> vectors,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a semantic search using vector similarity.
    /// </summary>
    /// <param name="queryEmbedding">The query embedding vector.</param>
    /// <param name="topK">The number of top results to return.</param>
    /// <param name="tenantId">The tenant ID to filter results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching results with their similarity scores and metadata.</returns>
    Task<List<(Guid Id, double Score, Dictionary<string, object> Payload)>> SearchAsync(
        float[] queryEmbedding,
        int topK,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a vector by ID.
    /// </summary>
    /// <param name="id">The vector ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteVectorAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all vectors associated with a document.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteDocumentVectorsAsync(
        Guid documentId, 
        Guid tenantId, 
        CancellationToken cancellationToken = default);
}
