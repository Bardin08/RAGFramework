using RAG.Core.DTOs.Admin;

namespace RAG.Application.Interfaces;

/// <summary>
/// Service interface for administrative operations.
/// </summary>
public interface IAdminService
{
    /// <summary>
    /// Retrieves system statistics.
    /// </summary>
    Task<SystemStatsResponse> GetSystemStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an index rebuild operation.
    /// </summary>
    Task<IndexRebuildResponse> StartIndexRebuildAsync(IndexRebuildRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of an index rebuild job.
    /// </summary>
    Task<IndexRebuildResponse?> GetRebuildStatusAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed health status of all dependencies.
    /// </summary>
    Task<DetailedHealthResponse> GetDetailedHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears specified caches.
    /// </summary>
    Task<CacheClearResponse> ClearCacheAsync(CacheClearRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a seed dataset for reproducible evaluations.
    /// </summary>
    Task<SeedDataResponse> LoadSeedDataAsync(SeedDataRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists available seed datasets.
    /// </summary>
    Task<AvailableSeedDatasetsResponse> ListAvailableSeedDatasetsAsync(CancellationToken cancellationToken = default);
}
