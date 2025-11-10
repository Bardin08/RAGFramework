using RAG.Core.Domain;

namespace RAG.Application.Interfaces;

/// <summary>
/// Repository for DocumentHash entity operations.
/// </summary>
public interface IDocumentHashRepository
{
    /// <summary>
    /// Gets a document hash by hash value and tenant ID.
    /// </summary>
    /// <param name="hash">The SHA-256 hash value</param>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The document hash if found, null otherwise</returns>
    Task<DocumentHash?> GetByHashAsync(string hash, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new document hash record.
    /// </summary>
    /// <param name="documentHash">The document hash to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddAsync(DocumentHash documentHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to add a new document hash record, handling race conditions.
    /// </summary>
    /// <param name="documentHash">The document hash to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if added successfully, false if duplicate constraint violation occurred</returns>
    Task<bool> TryAddAsync(DocumentHash documentHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a document hash exists for the given hash and tenant.
    /// </summary>
    /// <param name="hash">The SHA-256 hash value</param>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if exists, false otherwise</returns>
    Task<bool> ExistsAsync(string hash, Guid tenantId, CancellationToken cancellationToken = default);
}
