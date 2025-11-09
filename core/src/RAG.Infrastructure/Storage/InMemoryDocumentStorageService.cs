using System.Collections.Concurrent;
using RAG.Application.Interfaces;

namespace RAG.Infrastructure.Storage;

/// <summary>
/// In-memory implementation of document storage service for MVP.
/// Files are stored in memory for temporary processing.
/// </summary>
public class InMemoryDocumentStorageService : IDocumentStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _storage = new();

    /// <inheritdoc />
    public Task<string> SaveFileAsync(
        Guid documentId,
        Guid tenantId,
        string fileName,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        var key = GetStorageKey(documentId, tenantId);

        using var memoryStream = new MemoryStream();
        fileStream.CopyTo(memoryStream);
        var fileBytes = memoryStream.ToArray();

        _storage[key] = fileBytes;

        var extension = Path.GetExtension(fileName);
        var storagePath = $"memory://{tenantId}/{documentId}{extension}";

        return Task.FromResult(storagePath);
    }

    /// <inheritdoc />
    public Task<Stream?> GetFileAsync(
        Guid documentId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var key = GetStorageKey(documentId, tenantId);

        if (_storage.TryGetValue(key, out var fileBytes))
        {
            return Task.FromResult<Stream?>(new MemoryStream(fileBytes));
        }

        return Task.FromResult<Stream?>(null);
    }

    private static string GetStorageKey(Guid documentId, Guid tenantId) =>
        $"{tenantId}:{documentId}";
}
