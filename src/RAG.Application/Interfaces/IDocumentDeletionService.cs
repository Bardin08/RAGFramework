namespace RAG.Application.Interfaces;

/// <summary>
/// Service for orchestrating document deletion across all storage systems.
/// </summary>
public interface IDocumentDeletionService
{
    /// <summary>
    /// Deletes a document and all its associated data from all storage systems:
    /// Elasticsearch, Qdrant, PostgreSQL, and file storage.
    /// </summary>
    /// <param name="documentId">The document ID to delete.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="fileName">The original file name (for file storage deletion).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully, false if document not found or belongs to different tenant.</returns>
    Task<bool> DeleteDocumentAsync(
        Guid documentId,
        Guid tenantId,
        string fileName,
        CancellationToken cancellationToken = default);
}
