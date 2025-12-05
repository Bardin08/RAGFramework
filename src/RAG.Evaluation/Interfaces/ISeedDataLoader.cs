using RAG.Evaluation.Models;

namespace RAG.Evaluation.Interfaces;

/// <summary>
/// Service for loading seed datasets for reproducible evaluations.
/// </summary>
public interface ISeedDataLoader
{
    /// <summary>
    /// Loads a seed dataset by name from the data/seeds/ directory.
    /// </summary>
    /// <param name="datasetName">Name of the dataset (e.g., "dev-seed", "test-seed")</param>
    /// <param name="loadedBy">User ID who is loading the dataset</param>
    /// <param name="tenantId">Tenant ID to use for document indexing</param>
    /// <param name="forceReload">If true, reload even if dataset is already loaded with same hash</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with loaded dataset information</returns>
    Task<SeedDataLoadResult> LoadDatasetByNameAsync(
        string datasetName,
        Guid loadedBy,
        Guid tenantId,
        bool forceReload = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a seed dataset from inline JSON data.
    /// </summary>
    /// <param name="datasetJson">JSON string containing the seed dataset</param>
    /// <param name="loadedBy">User ID who is loading the dataset</param>
    /// <param name="tenantId">Tenant ID to use for document indexing</param>
    /// <param name="forceReload">If true, reload even if dataset is already loaded with same hash</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with loaded dataset information</returns>
    Task<SeedDataLoadResult> LoadDatasetFromJsonAsync(
        string datasetJson,
        Guid loadedBy,
        Guid tenantId,
        bool forceReload = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists available seed datasets in the data/seeds/ directory.
    /// </summary>
    /// <returns>List of available dataset names</returns>
    Task<List<string>> ListAvailableDatasetsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about a previously loaded dataset.
    /// </summary>
    /// <param name="datasetName">Name of the dataset</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dataset information or null if not found</returns>
    Task<Core.Domain.SeedDataset?> GetLoadedDatasetAsync(
        string datasetName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a seed data loading operation.
/// </summary>
public record SeedDataLoadResult
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Name of the loaded dataset.
    /// </summary>
    public required string DatasetName { get; init; }

    /// <summary>
    /// Number of documents loaded.
    /// </summary>
    public int DocumentsLoaded { get; init; }

    /// <summary>
    /// Number of queries in the dataset.
    /// </summary>
    public int QueriesCount { get; init; }

    /// <summary>
    /// Whether this was a fresh load or the dataset was already loaded.
    /// </summary>
    public bool WasAlreadyLoaded { get; init; }

    /// <summary>
    /// SHA-256 hash of the dataset.
    /// </summary>
    public string? Hash { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Validation errors if the dataset structure was invalid.
    /// </summary>
    public List<string>? ValidationErrors { get; init; }

    /// <summary>
    /// Time taken to load the dataset.
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Additional metadata about the load operation.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}
