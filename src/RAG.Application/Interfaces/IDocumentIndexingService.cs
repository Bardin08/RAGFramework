namespace RAG.Application.Interfaces;

/// <summary>
/// Defines a contract for orchestrating the full document indexing pipeline.
/// </summary>
public interface IDocumentIndexingService
{
    /// <summary>
    /// Processes a document through the full indexing pipeline:
    /// extract → chunk → embed → index (Elasticsearch + Qdrant + PostgreSQL).
    /// </summary>
    /// <param name="documentId">The document ID to process.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="ownerId">The user ID who owns the document.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="title">The document title.</param>
    /// <param name="source">The document source.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the indexing operation.</returns>
    Task IndexDocumentAsync(
        Guid documentId,
        Guid tenantId,
        Guid ownerId,
        string fileName,
        string title,
        string? source = null,
        CancellationToken cancellationToken = default);
}
