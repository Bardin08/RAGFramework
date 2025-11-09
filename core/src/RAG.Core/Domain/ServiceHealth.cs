namespace RAG.Core.Domain;

/// <summary>
/// Represents the health status of an individual service.
/// </summary>
public class ServiceHealth
{
    /// <summary>
    /// Gets or sets the status of the service (Healthy, Degraded, Unhealthy).
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Gets or sets the response time of the health check.
    /// </summary>
    public string? ResponseTime { get; set; }

    /// <summary>
    /// Gets or sets additional details about the service health.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets the number of indexed items (for search engines).
    /// </summary>
    public int? IndexCount { get; set; }

    /// <summary>
    /// Gets or sets the number of collections (for vector databases).
    /// </summary>
    public int? CollectionCount { get; set; }

    /// <summary>
    /// Gets or sets the loaded model name (for LLM services).
    /// </summary>
    public string? Model { get; set; }
}
