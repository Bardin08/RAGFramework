namespace RAG.Core.DTOs.Admin;

/// <summary>
/// Response containing system statistics.
/// </summary>
public class SystemStatsResponse
{
    /// <summary>
    /// Total number of documents in the system.
    /// </summary>
    public int TotalDocuments { get; init; }

    /// <summary>
    /// Total number of document chunks.
    /// </summary>
    public int TotalChunks { get; init; }

    /// <summary>
    /// Total number of queries processed.
    /// </summary>
    public long TotalQueriesProcessed { get; init; }

    /// <summary>
    /// Cache hit rate as a percentage (0-100).
    /// </summary>
    public double CacheHitRate { get; init; }

    /// <summary>
    /// Timestamp of the last index update.
    /// </summary>
    public DateTime? LastIndexUpdate { get; init; }

    /// <summary>
    /// System uptime as a formatted string.
    /// </summary>
    public string SystemUptime { get; init; } = string.Empty;

    /// <summary>
    /// Document count grouped by tenant.
    /// </summary>
    public Dictionary<Guid, int> DocumentsByTenant { get; init; } = new();
}
