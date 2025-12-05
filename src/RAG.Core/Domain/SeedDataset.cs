namespace RAG.Core.Domain;

/// <summary>
/// Represents a loaded seed dataset for reproducible evaluations.
/// Tracks which datasets have been loaded and their metadata.
/// </summary>
public class SeedDataset
{
    /// <summary>
    /// Gets the unique identifier for this seed dataset record.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the name of the seed dataset (e.g., "dev-seed", "test-seed", "benchmark").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the version of the seed dataset (e.g., "1.0", "2024.1").
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the SHA-256 hash of the dataset for idempotency checks.
    /// </summary>
    public string Hash { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the dataset was loaded.
    /// </summary>
    public DateTime LoadedAt { get; init; }

    /// <summary>
    /// Gets the number of documents loaded from this dataset.
    /// </summary>
    public int DocumentsCount { get; init; }

    /// <summary>
    /// Gets the number of queries in this dataset.
    /// </summary>
    public int QueriesCount { get; init; }

    /// <summary>
    /// Gets the ID of the user who loaded the dataset.
    /// </summary>
    public Guid LoadedBy { get; init; }

    /// <summary>
    /// Gets optional metadata about the dataset.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Creates a new instance of SeedDataset.
    /// </summary>
    /// <param name="id">Unique identifier</param>
    /// <param name="name">Dataset name</param>
    /// <param name="hash">SHA-256 hash value</param>
    /// <param name="loadedAt">Load timestamp</param>
    /// <param name="documentsCount">Number of documents</param>
    /// <param name="queriesCount">Number of queries</param>
    /// <param name="loadedBy">User ID who loaded the dataset</param>
    /// <param name="version">Dataset version</param>
    /// <param name="metadata">Optional metadata</param>
    public SeedDataset(
        Guid id,
        string name,
        string hash,
        DateTime loadedAt,
        int documentsCount,
        int queriesCount,
        Guid loadedBy,
        string? version = null,
        Dictionary<string, object>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Hash cannot be null or empty", nameof(hash));

        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty", nameof(id));

        if (loadedBy == Guid.Empty)
            throw new ArgumentException("LoadedBy cannot be empty", nameof(loadedBy));

        if (documentsCount < 0)
            throw new ArgumentException("DocumentsCount cannot be negative", nameof(documentsCount));

        if (queriesCount < 0)
            throw new ArgumentException("QueriesCount cannot be negative", nameof(queriesCount));

        Id = id;
        Name = name;
        Hash = hash;
        LoadedAt = loadedAt;
        DocumentsCount = documentsCount;
        QueriesCount = queriesCount;
        LoadedBy = loadedBy;
        Version = version;
        Metadata = metadata ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private SeedDataset()
    {
    }
}
