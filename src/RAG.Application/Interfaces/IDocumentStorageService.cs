namespace RAG.Application.Interfaces;

/// <summary>
/// Service for storing and retrieving uploaded document files.
/// </summary>
public interface IDocumentStorageService
{
    /// <summary>
    /// Saves a file to storage.
    /// </summary>
    /// <param name="documentId">The unique identifier for the document.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="fileStream">The file content stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The storage path or identifier.</returns>
    Task<string> SaveFileAsync(Guid documentId, Guid tenantId, string fileName, Stream fileStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a file from storage.
    /// </summary>
    /// <param name="documentId">The unique identifier for the document.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file content stream.</returns>
    Task<Stream?> GetFileAsync(Guid documentId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    /// <param name="documentId">The unique identifier for the document.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the deletion operation.</returns>
    Task DeleteFileAsync(Guid documentId, Guid tenantId, string fileName, CancellationToken cancellationToken = default);
}
