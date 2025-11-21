namespace RAG.Application.Interfaces;

/// <summary>
/// Client interface for MinIO object storage operations.
/// </summary>
public interface IMinIOClient
{
    /// <summary>
    /// Uploads a document to MinIO.
    /// </summary>
    Task<string> UploadDocumentAsync(
        Guid documentId,
        Guid tenantId,
        string fileName,
        Stream fileStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a document from MinIO.
    /// </summary>
    Task<Stream?> DownloadDocumentAsync(
        Guid documentId,
        Guid tenantId,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document from MinIO.
    /// </summary>
    Task<bool> DeleteDocumentAsync(
        Guid documentId,
        Guid tenantId,
        string fileName,
        CancellationToken cancellationToken = default);
}
