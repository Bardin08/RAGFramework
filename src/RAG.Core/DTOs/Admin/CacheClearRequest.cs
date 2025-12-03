namespace RAG.Core.DTOs.Admin;

/// <summary>
/// Request to clear system caches.
/// </summary>
public class CacheClearRequest
{
    /// <summary>
    /// Types of caches to clear. Valid values: "query", "embedding", "token", "health", "all".
    /// If empty or null, clears all caches.
    /// </summary>
    public List<string> CacheTypes { get; init; } = new() { "all" };
}
