using RAG.Core.Domain;

namespace RAG.Application.Interfaces;

/// <summary>
/// Repository interface for seed dataset persistence.
/// </summary>
public interface ISeedDatasetRepository
{
    /// <summary>
    /// Gets a seed dataset by name.
    /// </summary>
    Task<SeedDataset?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new seed dataset record.
    /// </summary>
    Task<SeedDataset> CreateAsync(SeedDataset dataset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a seed dataset by name.
    /// </summary>
    Task<bool> DeleteByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all loaded seed datasets.
    /// </summary>
    Task<IReadOnlyList<SeedDataset>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all documents, chunks, and hashes for a tenant.
    /// </summary>
    Task ClearTenantDataAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
