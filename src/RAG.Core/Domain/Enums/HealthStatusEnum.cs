namespace RAG.Core.Domain.Enums;

/// <summary>
/// Represents the health status of a service.
/// </summary>
public enum HealthStatusEnum
{
    /// <summary>
    /// Service is fully operational.
    /// </summary>
    Healthy,

    /// <summary>
    /// Service is operational but experiencing issues.
    /// </summary>
    Degraded,

    /// <summary>
    /// Service is not operational.
    /// </summary>
    Unhealthy
}
