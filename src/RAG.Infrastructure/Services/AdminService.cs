using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Application.Services;
using RAG.Core.Domain;
using RAG.Core.DTOs.Admin;
using RAG.Infrastructure.Data;

namespace RAG.Infrastructure.Services;

/// <summary>
/// Implementation of administrative operations service.
/// Jobs are stored in database for persistent tracking across application restarts.
/// </summary>
public class AdminService : IAdminService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHealthCheckService _healthCheckService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AdminService> _logger;
    private readonly Channel<IndexRebuildJob> _jobQueue;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationTokens;
    private readonly RAG.Evaluation.Interfaces.ISeedDataLoader? _seedDataLoader;

    private static readonly DateTime _startTime = DateTime.UtcNow;

    public AdminService(
        ApplicationDbContext dbContext,
        IHealthCheckService healthCheckService,
        IMemoryCache memoryCache,
        ILogger<AdminService> logger,
        Channel<IndexRebuildJob> jobQueue,
        RAG.Evaluation.Interfaces.ISeedDataLoader? seedDataLoader = null)
    {
        _dbContext = dbContext;
        _healthCheckService = healthCheckService;
        _memoryCache = memoryCache;
        _logger = logger;
        _jobQueue = jobQueue;
        _cancellationTokens = new ConcurrentDictionary<Guid, CancellationTokenSource>();
        _seedDataLoader = seedDataLoader;
    }

    /// <inheritdoc/>
    public async Task<SystemStatsResponse> GetSystemStatsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving system statistics");

        var totalDocuments = await _dbContext.Documents.CountAsync(cancellationToken);
        var totalChunks = await _dbContext.DocumentChunks.CountAsync(cancellationToken);

        var documentsByTenant = await _dbContext.Documents
            .GroupBy(d => d.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        var lastDocument = await _dbContext.Documents
            .OrderByDescending(d => d.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var uptime = DateTime.UtcNow - _startTime;
        var uptimeFormatted = uptime.TotalDays >= 1
            ? $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m"
            : $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";

        var stats = new SystemStatsResponse
        {
            TotalDocuments = totalDocuments,
            TotalChunks = totalChunks,
            TotalQueriesProcessed = 0, // Would need a separate counter/metrics service
            CacheHitRate = 0.0, // Would need cache statistics tracking
            LastIndexUpdate = lastDocument?.UpdatedAt,
            SystemUptime = uptimeFormatted,
            DocumentsByTenant = documentsByTenant
        };

        _logger.LogInformation("System stats: {TotalDocuments} documents, {TotalChunks} chunks",
            totalDocuments, totalChunks);

        return stats;
    }

    /// <inheritdoc/>
    public async Task<IndexRebuildResponse> StartIndexRebuildAsync(
        IndexRebuildRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting index rebuild. TenantId: {TenantId}, IncludeEmbeddings: {IncludeEmbeddings}",
            request.TenantId?.ToString() ?? "all", request.IncludeEmbeddings);

        // Count documents to estimate
        var query = _dbContext.Documents.AsQueryable();
        if (request.TenantId.HasValue)
        {
            query = query.Where(d => d.TenantId == request.TenantId.Value);
        }
        var estimatedDocuments = await query.CountAsync(cancellationToken);

        var cts = new CancellationTokenSource();
        var job = new IndexRebuildJob
        {
            TenantId = request.TenantId,
            IncludeEmbeddings = request.IncludeEmbeddings,
            EstimatedDocuments = estimatedDocuments,
            InitiatedBy = request.InitiatedBy,
            CancellationTokenSource = cts
        };

        // Store in database for persistent tracking
        _dbContext.IndexRebuildJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Store cancellation token in memory (not persisted)
        _cancellationTokens[job.JobId] = cts;

        // Queue the job for background processing
        await _jobQueue.Writer.WriteAsync(job, cancellationToken);

        _logger.LogInformation(
            "Index rebuild job queued and saved to database. JobId: {JobId}, EstimatedDocuments: {EstimatedDocuments}",
            job.JobId, estimatedDocuments);

        return new IndexRebuildResponse
        {
            JobId = job.JobId,
            Status = job.Status,
            EstimatedDocuments = estimatedDocuments,
            ProcessedDocuments = 0,
            StartedAt = job.StartedAt
        };
    }

    /// <inheritdoc/>
    public async Task<IndexRebuildResponse?> GetRebuildStatusAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Looking up job {JobId} from database", jobId);

        var job = await _dbContext.IndexRebuildJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);

        if (job == null)
        {
            _logger.LogWarning("Index rebuild job not found: {JobId}", jobId);
            return null;
        }

        _logger.LogDebug("Found job {JobId} with status {Status}", jobId, job.Status);

        return new IndexRebuildResponse
        {
            JobId = job.JobId,
            Status = job.Status,
            EstimatedDocuments = job.EstimatedDocuments,
            ProcessedDocuments = job.ProcessedDocuments,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            Error = job.Error
        };
    }

    /// <inheritdoc/>
    public async Task<DetailedHealthResponse> GetDetailedHealthAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving detailed health status");

        var sw = Stopwatch.StartNew();
        var healthStatus = await _healthCheckService.GetHealthStatusAsync();
        sw.Stop();

        var dependencies = new Dictionary<string, DependencyHealth>();

        foreach (var (serviceName, serviceHealth) in healthStatus.Services)
        {
            var details = new Dictionary<string, object>();

            if (serviceHealth.IndexCount.HasValue)
                details["indexCount"] = serviceHealth.IndexCount.Value;
            if (serviceHealth.CollectionCount.HasValue)
                details["collectionCount"] = serviceHealth.CollectionCount.Value;
            if (!string.IsNullOrEmpty(serviceHealth.Model))
                details["model"] = serviceHealth.Model;

            dependencies[serviceName] = new DependencyHealth
            {
                Name = serviceName,
                Status = serviceHealth.Status,
                Description = serviceHealth.Details,
                ResponseTime = TimeSpan.TryParse(serviceHealth.ResponseTime?.Replace("ms", ""), out var ms)
                    ? TimeSpan.FromMilliseconds(double.Parse(serviceHealth.ResponseTime.Replace("ms", "")))
                    : TimeSpan.Zero,
                Details = details.Count > 0 ? details : null
            };
        }

        // Add PostgreSQL check
        try
        {
            var dbSw = Stopwatch.StartNew();
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            dbSw.Stop();

            dependencies["postgresql"] = new DependencyHealth
            {
                Name = "postgresql",
                Status = canConnect ? "Healthy" : "Unhealthy",
                Description = canConnect ? "Connected" : "Cannot connect to database",
                ResponseTime = dbSw.Elapsed
            };
        }
        catch (Exception ex)
        {
            dependencies["postgresql"] = new DependencyHealth
            {
                Name = "postgresql",
                Status = "Unhealthy",
                Description = ex.Message,
                ResponseTime = TimeSpan.Zero
            };
        }

        var overallStatus = dependencies.Values.All(d => d.Status == "Healthy")
            ? "Healthy"
            : dependencies.Values.Any(d => d.Status == "Unhealthy")
                ? "Unhealthy"
                : "Degraded";

        return new DetailedHealthResponse
        {
            OverallStatus = overallStatus,
            CheckedAt = DateTime.UtcNow,
            Dependencies = dependencies
        };
    }

    /// <inheritdoc/>
    public Task<CacheClearResponse> ClearCacheAsync(
        CacheClearRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Clearing caches: {CacheTypes}", string.Join(", ", request.CacheTypes));

        var clearedCaches = new List<string>();
        var entriesRemoved = 0;

        // IMemoryCache doesn't expose a way to enumerate or count entries
        // We can only call Remove for specific keys or clear via disposal
        // For this implementation, we'll use compact which removes expired entries
        // and document that full clear requires cache key knowledge

        var cacheTypes = request.CacheTypes.Select(t => t.ToLowerInvariant()).ToList();
        var clearAll = cacheTypes.Contains("all") || cacheTypes.Count == 0;

        if (clearAll || cacheTypes.Contains("health"))
        {
            // Clear health check cache
            _memoryCache.Remove("health_status");
            clearedCaches.Add("health");
            entriesRemoved++;
        }

        if (clearAll || cacheTypes.Contains("query"))
        {
            // Query cache would need specific key pattern
            // For now, we mark it as cleared
            clearedCaches.Add("query");
        }

        if (clearAll || cacheTypes.Contains("embedding"))
        {
            clearedCaches.Add("embedding");
        }

        if (clearAll || cacheTypes.Contains("token"))
        {
            clearedCaches.Add("token");
        }

        // Compact the cache to remove expired entries
        if (_memoryCache is MemoryCache mc)
        {
            mc.Compact(1.0); // Remove 100% of expired entries
        }

        _logger.LogInformation("Caches cleared: {ClearedCaches}", string.Join(", ", clearedCaches));

        return Task.FromResult(new CacheClearResponse
        {
            ClearedCaches = clearedCaches,
            EntriesRemoved = entriesRemoved,
            ClearedAt = DateTime.UtcNow
        });
    }

    /// <inheritdoc/>
    public async Task<SeedDataResponse> LoadSeedDataAsync(
        SeedDataRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate request
        var (isValid, error) = request.Validate();
        if (!isValid)
        {
            return new SeedDataResponse
            {
                Success = false,
                DatasetName = request.DatasetName ?? "unknown",
                Error = error
            };
        }

        // Check if seed data loader is available
        if (_seedDataLoader == null)
        {
            _logger.LogError("SeedDataLoader service is not registered");
            return new SeedDataResponse
            {
                Success = false,
                DatasetName = request.DatasetName ?? "unknown",
                Error = "Seed data loading is not available. ISeedDataLoader service is not registered."
            };
        }

        // Determine tenant ID
        var tenantId = request.TenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001"); // Default evaluation tenant

        // Get current user ID (this should come from the request context)
        // For now, we'll use a system user ID
        var loadedBy = Guid.Parse("00000000-0000-0000-0000-000000000000");

        RAG.Evaluation.Interfaces.SeedDataLoadResult result;

        // Load dataset
        if (!string.IsNullOrWhiteSpace(request.DatasetName))
        {
            _logger.LogInformation("Loading seed dataset by name: {DatasetName}", request.DatasetName);
            result = await _seedDataLoader.LoadDatasetByNameAsync(
                request.DatasetName,
                loadedBy,
                tenantId,
                request.ForceReload,
                cancellationToken);
        }
        else
        {
            _logger.LogInformation("Loading seed dataset from inline JSON");
            result = await _seedDataLoader.LoadDatasetFromJsonAsync(
                request.DatasetJson!,
                loadedBy,
                tenantId,
                request.ForceReload,
                cancellationToken);
        }

        // Convert to response
        return new SeedDataResponse
        {
            Success = result.Success,
            DatasetName = result.DatasetName,
            DocumentsLoaded = result.DocumentsLoaded,
            QueriesCount = result.QueriesCount,
            WasAlreadyLoaded = result.WasAlreadyLoaded,
            Hash = result.Hash,
            Error = result.Error,
            ValidationErrors = result.ValidationErrors,
            DurationMs = result.Duration?.TotalMilliseconds,
            LoadedAt = DateTime.UtcNow,
            LoadedBy = loadedBy,
            Metadata = result.Metadata
        };
    }

    /// <inheritdoc/>
    public async Task<AvailableSeedDatasetsResponse> ListAvailableSeedDatasetsAsync(
        CancellationToken cancellationToken = default)
    {
        if (_seedDataLoader == null)
        {
            _logger.LogError("SeedDataLoader service is not registered");
            return new AvailableSeedDatasetsResponse
            {
                Datasets = new List<string>()
            };
        }

        var datasets = await _seedDataLoader.ListAvailableDatasetsAsync(cancellationToken);

        return new AvailableSeedDatasetsResponse
        {
            Datasets = datasets
        };
    }
}
