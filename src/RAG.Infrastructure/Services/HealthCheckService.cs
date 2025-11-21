using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RAG.Application.Services;
using RAG.Core.Domain;

namespace RAG.Infrastructure.Services;

/// <summary>
/// Implementation of health check service that monitors all RAG system dependencies.
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HealthCheckService> _logger;

    private const int TimeoutSeconds = 5;
    private const int CacheDurationSeconds = 10;
    private const string CacheKey = "health_status";

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="cache">The memory cache.</param>
    /// <param name="logger">The logger.</param>
    public HealthCheckService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<HealthCheckService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<HealthStatus> GetHealthStatusAsync()
    {
        // Check cache first
        if (_cache.TryGetValue(CacheKey, out HealthStatus? cachedStatus) && cachedStatus != null)
        {
            _logger.LogDebug("Returning cached health status");
            return cachedStatus;
        }

        _logger.LogInformation("Performing health checks on all services");

        var services = new Dictionary<string, ServiceHealth>();

        // Check each service with timeout
        await Task.WhenAll(
            CheckElasticsearch(services),
            CheckQdrant(services),
            CheckKeycloak(services),
            CheckEmbeddingService(services)
            // Note: Ollama and Postgres checks will be added in future stories
        );

        var overallStatus = services.Values.All(s => s.Status == "Healthy")
            ? "Healthy"
            : "Unhealthy";

        var healthStatus = new HealthStatus
        {
            Status = overallStatus,
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Services = services
        };

        // Cache result
        _cache.Set(CacheKey, healthStatus, TimeSpan.FromSeconds(CacheDurationSeconds));

        _logger.LogInformation("Health check completed. Overall status: {Status}", overallStatus);

        return healthStatus;
    }

    private async Task CheckElasticsearch(Dictionary<string, ServiceHealth> services)
    {
        const string serviceName = "elasticsearch";
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);

            var sw = Stopwatch.StartNew();
            var response = await client.GetAsync("http://localhost:9200/_cluster/health");
            sw.Stop();

            services[serviceName] = new ServiceHealth
            {
                Status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy",
                ResponseTime = $"{sw.ElapsedMilliseconds}ms"
            };

            _logger.LogDebug("Elasticsearch health check: {Status} ({ResponseTime})",
                services[serviceName].Status, services[serviceName].ResponseTime);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Elasticsearch health check failed");
            services[serviceName] = new ServiceHealth
            {
                Status = "Unhealthy",
                Details = ex.Message
            };
        }
    }

    private async Task CheckQdrant(Dictionary<string, ServiceHealth> services)
    {
        const string serviceName = "qdrant";
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);

            var sw = Stopwatch.StartNew();
            var response = await client.GetAsync("http://localhost:6333/healthz");
            sw.Stop();

            services[serviceName] = new ServiceHealth
            {
                Status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy",
                ResponseTime = $"{sw.ElapsedMilliseconds}ms"
            };

            _logger.LogDebug("Qdrant health check: {Status} ({ResponseTime})",
                services[serviceName].Status, services[serviceName].ResponseTime);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Qdrant health check failed");
            services[serviceName] = new ServiceHealth
            {
                Status = "Unhealthy",
                Details = ex.Message
            };
        }
    }

    private async Task CheckKeycloak(Dictionary<string, ServiceHealth> services)
    {
        const string serviceName = "keycloak";
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);

            var sw = Stopwatch.StartNew();
            var response = await client.GetAsync("http://localhost:9000/health");
            sw.Stop();

            services[serviceName] = new ServiceHealth
            {
                Status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy",
                ResponseTime = $"{sw.ElapsedMilliseconds}ms"
            };

            _logger.LogDebug("Keycloak health check: {Status} ({ResponseTime})",
                services[serviceName].Status, services[serviceName].ResponseTime);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keycloak health check failed");
            services[serviceName] = new ServiceHealth
            {
                Status = "Unhealthy",
                Details = ex.Message
            };
        }
    }

    private async Task CheckEmbeddingService(Dictionary<string, ServiceHealth> services)
    {
        const string serviceName = "embeddingService";
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);

            var sw = Stopwatch.StartNew();
            var response = await client.GetAsync("http://localhost:8001/health");
            sw.Stop();

            services[serviceName] = new ServiceHealth
            {
                Status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy",
                ResponseTime = $"{sw.ElapsedMilliseconds}ms"
            };

            _logger.LogDebug("Embedding Service health check: {Status} ({ResponseTime})",
                services[serviceName].Status, services[serviceName].ResponseTime);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding Service health check failed");
            services[serviceName] = new ServiceHealth
            {
                Status = "Unhealthy",
                Details = ex.Message
            };
        }
    }
}
