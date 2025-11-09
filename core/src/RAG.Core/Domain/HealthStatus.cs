namespace RAG.Core.Domain;

/// <summary>
/// Represents the overall health status of the RAG system.
/// </summary>
public class HealthStatus
{
    /// <summary>
    /// Gets or sets the overall status (Healthy, Degraded, Unhealthy).
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the health check was performed.
    /// </summary>
    public required DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the application version.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Gets or sets the health status of individual services.
    /// </summary>
    public required Dictionary<string, ServiceHealth> Services { get; set; }
}
