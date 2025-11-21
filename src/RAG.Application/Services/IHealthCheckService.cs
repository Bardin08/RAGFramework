using RAG.Core.Domain;

namespace RAG.Application.Services;

/// <summary>
/// Interface for health check operations.
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// Gets the current health status of the RAG system and all its dependencies.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the health status.</returns>
    Task<HealthStatus> GetHealthStatusAsync();
}
