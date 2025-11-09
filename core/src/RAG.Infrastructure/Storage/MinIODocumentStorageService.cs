using System.Collections.Concurrent;
using RAG.Application.Interfaces;

namespace RAG.Infrastructure.Storage;

/// <summary>
/// MinIO-based implementation of document storage service.
/// </summary>
public class MinIODocumentStorageService : IDocumentStorageService
{
    private readonly IMinIOClient _minioClient;
    private readonly ConcurrentDictionary<string, string> _fileNameCache = new();

    public MinIODocumentStorageService(IMinIOClient minioClient)
    {
        _minioClient = minioClient;
    }

    /// <inheritdoc />
    public async Task<string> SaveFileAsync(
        Guid documentId,
        Guid tenantId,
        string fileName,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        // Store fileName in cache for later retrieval
        var key = GetCacheKey(documentId, tenantId);
        _fileNameCache[key] = fileName;

        return await _minioClient.UploadDocumentAsync(
            documentId,
            tenantId,
            fileName,
            fileStream,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Stream?> GetFileAsync(
        Guid documentId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // Retrieve fileName from cache
        var key = GetCacheKey(documentId, tenantId);
        if (!_fileNameCache.TryGetValue(key, out var fileName))
        {
            return null;
        }

        return await _minioClient.DownloadDocumentAsync(
            documentId,
            tenantId,
            fileName,
            cancellationToken);
    }

    private static string GetCacheKey(Guid documentId, Guid tenantId) =>
        $"{tenantId}:{documentId}";
}
