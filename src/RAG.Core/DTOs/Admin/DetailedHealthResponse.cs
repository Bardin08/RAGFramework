namespace RAG.Core.DTOs.Admin;

/// <summary>
/// Detailed health status response for all system dependencies.
/// </summary>
public class DetailedHealthResponse
{
    /// <summary>
    /// Overall system status: Healthy, Degraded, or Unhealthy.
    /// </summary>
    public string OverallStatus { get; init; } = "Unknown";

    /// <summary>
    /// Timestamp when the health check was performed.
    /// </summary>
    public DateTime CheckedAt { get; init; }

    /// <summary>
    /// Health status of individual dependencies.
    /// </summary>
    public Dictionary<string, DependencyHealth> Dependencies { get; init; } = new();
}

/// <summary>
/// Health status for a single dependency.
/// </summary>
public class DependencyHealth
{
    /// <summary>
    /// Name of the dependency.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Status: Healthy, Degraded, or Unhealthy.
    /// </summary>
    public string Status { get; init; } = "Unknown";

    /// <summary>
    /// Human-readable description of the health status.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Response time of the health check.
    /// </summary>
    public TimeSpan ResponseTime { get; init; }

    /// <summary>
    /// Version of the dependency (if available).
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Additional details specific to the dependency type.
    /// </summary>
    public Dictionary<string, object>? Details { get; init; }
}
