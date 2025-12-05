namespace RAG.Core.DTOs.Admin;

/// <summary>
/// Response from loading a seed dataset.
/// </summary>
public class SeedDataResponse
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Name of the dataset that was loaded.
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
    /// Whether this dataset was already loaded (not reloaded).
    /// </summary>
    public bool WasAlreadyLoaded { get; init; }

    /// <summary>
    /// SHA-256 hash of the dataset for tracking.
    /// </summary>
    public string? Hash { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Validation errors if the dataset was invalid.
    /// </summary>
    public List<string>? ValidationErrors { get; init; }

    /// <summary>
    /// Time taken to load the dataset.
    /// </summary>
    public double? DurationMs { get; init; }

    /// <summary>
    /// Timestamp when the dataset was loaded.
    /// </summary>
    public DateTime? LoadedAt { get; init; }

    /// <summary>
    /// ID of the user who loaded the dataset.
    /// </summary>
    public Guid? LoadedBy { get; init; }

    /// <summary>
    /// Additional metadata about the load operation.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Response listing available seed datasets.
/// </summary>
public class AvailableSeedDatasetsResponse
{
    /// <summary>
    /// List of available dataset names.
    /// </summary>
    public required List<string> Datasets { get; init; }

    /// <summary>
    /// Number of available datasets.
    /// </summary>
    public int Count => Datasets.Count;
}
