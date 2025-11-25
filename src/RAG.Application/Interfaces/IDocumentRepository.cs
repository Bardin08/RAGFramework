using RAG.Core.Domain;

namespace RAG.Application.Interfaces;

/// <summary>
/// Repository for document data access operations.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>
    /// Gets a paginated list of documents for a tenant with optional search filtering.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="skip">Number of documents to skip (for pagination).</param>
    /// <param name="take">Number of documents to take (page size).</param>
    /// <param name="searchTerm">Optional search term to filter by title.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of (documents, totalCount).</returns>
    Task<(List<Document> Documents, int TotalCount)> GetDocumentsAsync(
        Guid tenantId,
        int skip,
        int take,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a document by ID with its chunks.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document with chunks, or null if not found or wrong tenant.</returns>
    Task<Document?> GetDocumentByIdAsync(
        Guid documentId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all chunks for a document.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of document chunks.</returns>
    Task<List<DocumentChunk>> GetDocumentChunksAsync(
        Guid documentId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document and its chunks from the database.
    /// </summary>
    /// <param name="documentId">The document ID to delete.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted, false if not found or wrong tenant.</returns>
    Task<bool> DeleteDocumentAsync(
        Guid documentId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a document with its chunks to the database.
    /// </summary>
    /// <param name="document">The document to add.</param>
    /// <param name="chunks">The chunks belonging to the document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddDocumentWithChunksAsync(
        Document document,
        List<DocumentChunk> chunks,
        CancellationToken cancellationToken = default);
}
