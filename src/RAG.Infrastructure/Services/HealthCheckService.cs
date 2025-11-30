using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Application.Services;
using RAG.Core.Configuration;
using RAG.Core.Domain;
using RAG.Core.Interfaces;

namespace RAG.Infrastructure.Services;

/// <summary>
/// Implementation of health check service that monitors all RAG system dependencies.
/// AC 6: Includes LLM provider availability checks.
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HealthCheckService> _logger;
    private readonly AppSettings _appSettings;
    private readonly IEnumerable<ILLMProvider> _llmProviders;

    private const int TimeoutSeconds = 5;
    private const int CacheDurationSeconds = 10;
    private const string CacheKey = "health_status";

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="cache">The memory cache.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="appSettings">The application settings.</param>
    /// <param name="llmProviders">The LLM providers for health checks (AC 6).</param>
    public HealthCheckService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<HealthCheckService> logger,
        IOptions<AppSettings> appSettings,
        IEnumerable<ILLMProvider> llmProviders)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
        _appSettings = appSettings.Value;
        _llmProviders = llmProviders;
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
            CheckEmbeddingService(services),
            CheckLLMProviders(services) // AC 6: LLM provider availability
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
            var healthUrl = $"{_appSettings.Elasticsearch.Url}/_cluster/health";
            var response = await client.GetAsync(healthUrl);
            sw.Stop();

            int? indexCount = null;
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var catIndicesUrl = $"{_appSettings.Elasticsearch.Url}/_cat/indices?format=json";
                    var indicesResponse = await client.GetAsync(catIndicesUrl);
                    if (indicesResponse.IsSuccessStatusCode)
                    {
                        var indicesJson = await indicesResponse.Content.ReadAsStringAsync();
                        using var doc = System.Text.Json.JsonDocument.Parse(indicesJson);
                        indexCount = doc.RootElement.GetArrayLength();
                    }
                }
                catch { /* Ignore metadata errors */ }
            }

            services[serviceName] = new ServiceHealth
            {
                Status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy",
                ResponseTime = $"{sw.ElapsedMilliseconds}ms",
                IndexCount = indexCount
            };

            _logger.LogDebug("Elasticsearch health check: {Status} ({ResponseTime}), Indices: {IndexCount}",
                services[serviceName].Status, services[serviceName].ResponseTime, indexCount);
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

            // Qdrant HTTP API is on port 6333, gRPC is on 6334
            // The config URL points to gRPC (6334), so we need to adjust for HTTP health checks
            var httpUrl = _appSettings.Qdrant.Url.Replace(":6334", ":6333");

            var sw = Stopwatch.StartNew();
            var healthUrl = $"{httpUrl}/healthz";
            var response = await client.GetAsync(healthUrl);
            sw.Stop();

            int? collectionCount = null;
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var collectionsUrl = $"{httpUrl}/collections";
                    var collectionsResponse = await client.GetAsync(collectionsUrl);
                    if (collectionsResponse.IsSuccessStatusCode)
                    {
                        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
                        using var doc = System.Text.Json.JsonDocument.Parse(collectionsJson);
                        if (doc.RootElement.TryGetProperty("result", out var result) &&
                            result.TryGetProperty("collections", out var collections))
                        {
                            collectionCount = collections.GetArrayLength();
                        }
                    }
                }
                catch { /* Ignore metadata errors */ }
            }

            services[serviceName] = new ServiceHealth
            {
                Status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy",
                ResponseTime = $"{sw.ElapsedMilliseconds}ms",
                CollectionCount = collectionCount
            };

            _logger.LogDebug("Qdrant health check: {Status} ({ResponseTime}), Collections: {CollectionCount}",
                services[serviceName].Status, services[serviceName].ResponseTime, collectionCount);
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
            // Keycloak health endpoint is at /health/ready on management port (9000)
            var baseUrl = _appSettings.Keycloak.Authority.Replace("/realms/rag-system", "").Replace(":8080", ":9000");
            var healthUrl = $"{baseUrl}/health/ready";
            var response = await client.GetAsync(healthUrl);
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
            var healthUrl = $"{_appSettings.EmbeddingService.Url}/health";
            var response = await client.GetAsync(healthUrl);
            sw.Stop();

            string? model = null;
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var healthJson = await response.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(healthJson);
                    if (doc.RootElement.TryGetProperty("model", out var modelProp))
                    {
                        model = modelProp.GetString();
                    }
                }
                catch { /* Ignore metadata errors */ }
            }

            services[serviceName] = new ServiceHealth
            {
                Status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy",
                ResponseTime = $"{sw.ElapsedMilliseconds}ms",
                Model = model
            };

            _logger.LogDebug("Embedding Service health check: {Status} ({ResponseTime}), Model: {Model}",
                services[serviceName].Status, services[serviceName].ResponseTime, model);
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

    /// <summary>
    /// AC 6: Check LLM provider availability (OpenAI, Ollama)
    /// </summary>
    private Task CheckLLMProviders(Dictionary<string, ServiceHealth> services)
    {
        foreach (var provider in _llmProviders)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var isAvailable = provider.IsAvailable;
                sw.Stop();

                var serviceName = $"llm-{provider.ProviderName.ToLowerInvariant()}";

                // Get model name from configuration
                string? model = provider.ProviderName.ToLowerInvariant() switch
                {
                    "openai" => _appSettings.OpenAI.Model,
                    "ollama" => _appSettings.Ollama.Model,
                    _ => null
                };

                services[serviceName] = new ServiceHealth
                {
                    Status = isAvailable ? "Healthy" : "Unhealthy",
                    ResponseTime = $"{sw.ElapsedMilliseconds}ms",
                    Details = isAvailable ? null : "Provider not available",
                    Model = model
                };

                _logger.LogDebug("{ProviderName} health check: {Status} ({ResponseTime}), Model: {Model}",
                    provider.ProviderName, services[serviceName].Status, services[serviceName].ResponseTime, model);
            }
            catch (Exception ex)
            {
                var serviceName = $"llm-{provider.ProviderName.ToLowerInvariant()}";
                _logger.LogWarning(ex, "{ProviderName} health check failed", provider.ProviderName);
                services[serviceName] = new ServiceHealth
                {
                    Status = "Unhealthy",
                    Details = ex.Message
                };
            }
        }

        return Task.CompletedTask;
    }
}
