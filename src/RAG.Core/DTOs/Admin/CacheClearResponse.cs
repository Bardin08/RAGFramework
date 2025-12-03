namespace RAG.Core.DTOs.Admin;

/// <summary>
/// Response for cache clear operation.
/// </summary>
public class CacheClearResponse
{
    /// <summary>
    /// List of cache types that were cleared.
    /// </summary>
    public List<string> ClearedCaches { get; init; } = new();

    /// <summary>
    /// Approximate number of entries removed.
    /// </summary>
    public int EntriesRemoved { get; init; }

    /// <summary>
    /// Timestamp when the cache was cleared.
    /// </summary>
    public DateTime ClearedAt { get; init; }
}
