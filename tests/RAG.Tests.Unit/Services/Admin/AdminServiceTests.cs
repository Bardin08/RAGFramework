using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.Application.Services;
using RAG.Core.Domain;
using RAG.Core.DTOs.Admin;
using RAG.Infrastructure.Data;
using RAG.Infrastructure.Services;
using Shouldly;

namespace RAG.Tests.Unit.Services.Admin;

/// <summary>
/// Unit tests for AdminService.
/// </summary>
public class AdminServiceTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Mock<IHealthCheckService> _healthCheckServiceMock;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<AdminService>> _loggerMock;
    private readonly Channel<IndexRebuildJob> _jobQueue;
    private readonly ConcurrentDictionary<Guid, IndexRebuildJob> _jobTracker;
    private readonly AdminService _service;

    public AdminServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _healthCheckServiceMock = new Mock<IHealthCheckService>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<AdminService>>();
        _jobQueue = Channel.CreateUnbounded<IndexRebuildJob>();
        _jobTracker = new ConcurrentDictionary<Guid, IndexRebuildJob>();

        _service = new AdminService(
            _dbContext,
            _healthCheckServiceMock.Object,
            _memoryCache,
            _loggerMock.Object,
            _jobQueue,
            _jobTracker);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _memoryCache.Dispose();
    }

    #region GetSystemStatsAsync Tests

    [Fact]
    public async Task GetSystemStatsAsync_ReturnsZeroCounts_WhenDatabaseIsEmpty()
    {
        // Act
        var result = await _service.GetSystemStatsAsync();

        // Assert
        result.ShouldNotBeNull();
        result.TotalDocuments.ShouldBe(0);
        result.TotalChunks.ShouldBe(0);
        result.DocumentsByTenant.ShouldBeEmpty();
        result.SystemUptime.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetSystemStatsAsync_ReturnsCorrectCounts_WhenDocumentsExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var document = new Document(docId, "Test Document", "Test Content", tenantId);
        _dbContext.Documents.Add(document);

        var chunk = new DocumentChunk(Guid.NewGuid(), docId, "Test chunk", 0, 10, 0, tenantId);
        _dbContext.DocumentChunks.Add(chunk);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetSystemStatsAsync();

        // Assert
        result.TotalDocuments.ShouldBe(1);
        result.TotalChunks.ShouldBe(1);
        result.DocumentsByTenant.ShouldContainKey(tenantId);
        result.DocumentsByTenant[tenantId].ShouldBe(1);
        result.LastIndexUpdate.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetSystemStatsAsync_ReturnsCorrectCountsByTenant_WhenMultipleTenantsExist()
    {
        // Arrange
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        _dbContext.Documents.AddRange(
            new Document(Guid.NewGuid(), "Doc1", "Content", tenant1),
            new Document(Guid.NewGuid(), "Doc2", "Content", tenant1),
            new Document(Guid.NewGuid(), "Doc3", "Content", tenant2)
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetSystemStatsAsync();

        // Assert
        result.TotalDocuments.ShouldBe(3);
        result.DocumentsByTenant[tenant1].ShouldBe(2);
        result.DocumentsByTenant[tenant2].ShouldBe(1);
    }

    #endregion

    #region StartIndexRebuildAsync Tests

    [Fact]
    public async Task StartIndexRebuildAsync_ReturnsJobId_WhenRequestIsValid()
    {
        // Arrange
        var request = new IndexRebuildRequest { IncludeEmbeddings = true };

        // Act
        var result = await _service.StartIndexRebuildAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.JobId.ShouldNotBe(Guid.Empty);
        result.Status.ShouldBe("Queued");
        result.EstimatedDocuments.ShouldBe(0);
    }

    [Fact]
    public async Task StartIndexRebuildAsync_TracksJob_InJobTracker()
    {
        // Arrange
        var request = new IndexRebuildRequest();

        // Act
        var result = await _service.StartIndexRebuildAsync(request);

        // Assert
        _jobTracker.ShouldContainKey(result.JobId);
        _jobTracker[result.JobId].Status.ShouldBe("Queued");
    }

    [Fact]
    public async Task StartIndexRebuildAsync_QueuesJob_InChannel()
    {
        // Arrange
        var request = new IndexRebuildRequest();

        // Act
        var result = await _service.StartIndexRebuildAsync(request);

        // Assert
        var jobFromQueue = await _jobQueue.Reader.ReadAsync();
        jobFromQueue.JobId.ShouldBe(result.JobId);
    }

    [Fact]
    public async Task StartIndexRebuildAsync_FiltersByTenant_WhenTenantIdProvided()
    {
        // Arrange
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        _dbContext.Documents.AddRange(
            new Document(Guid.NewGuid(), "Doc1", "Content", tenant1),
            new Document(Guid.NewGuid(), "Doc2", "Content", tenant2)
        );
        await _dbContext.SaveChangesAsync();

        var request = new IndexRebuildRequest { TenantId = tenant1 };

        // Act
        var result = await _service.StartIndexRebuildAsync(request);

        // Assert
        result.EstimatedDocuments.ShouldBe(1);
    }

    #endregion

    #region GetRebuildStatusAsync Tests

    [Fact]
    public async Task GetRebuildStatusAsync_ReturnsNull_WhenJobNotFound()
    {
        // Arrange
        var nonExistentJobId = Guid.NewGuid();

        // Act
        var result = await _service.GetRebuildStatusAsync(nonExistentJobId);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetRebuildStatusAsync_ReturnsJobStatus_WhenJobExists()
    {
        // Arrange
        var request = new IndexRebuildRequest();
        var startResult = await _service.StartIndexRebuildAsync(request);

        // Act
        var result = await _service.GetRebuildStatusAsync(startResult.JobId);

        // Assert
        result.ShouldNotBeNull();
        result!.JobId.ShouldBe(startResult.JobId);
        result.Status.ShouldBe("Queued");
    }

    #endregion

    #region GetDetailedHealthAsync Tests

    [Fact]
    public async Task GetDetailedHealthAsync_ReturnsHealthStatus_FromHealthCheckService()
    {
        // Arrange
        var healthStatus = new HealthStatus
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Services = new Dictionary<string, ServiceHealth>
            {
                ["elasticsearch"] = new ServiceHealth { Status = "Healthy", ResponseTime = "10ms" },
                ["qdrant"] = new ServiceHealth { Status = "Healthy", ResponseTime = "5ms" }
            }
        };
        _healthCheckServiceMock.Setup(x => x.GetHealthStatusAsync())
            .ReturnsAsync(healthStatus);

        // Act
        var result = await _service.GetDetailedHealthAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Dependencies.ShouldContainKey("elasticsearch");
        result.Dependencies.ShouldContainKey("qdrant");
        result.Dependencies.ShouldContainKey("postgresql");
    }

    [Fact]
    public async Task GetDetailedHealthAsync_ReturnsUnhealthy_WhenAnyServiceIsUnhealthy()
    {
        // Arrange
        var healthStatus = new HealthStatus
        {
            Status = "Unhealthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Services = new Dictionary<string, ServiceHealth>
            {
                ["elasticsearch"] = new ServiceHealth { Status = "Unhealthy", Details = "Connection refused" },
                ["qdrant"] = new ServiceHealth { Status = "Healthy" }
            }
        };
        _healthCheckServiceMock.Setup(x => x.GetHealthStatusAsync())
            .ReturnsAsync(healthStatus);

        // Act
        var result = await _service.GetDetailedHealthAsync();

        // Assert
        result.OverallStatus.ShouldBe("Unhealthy");
    }

    #endregion

    #region ClearCacheAsync Tests

    [Fact]
    public async Task ClearCacheAsync_ClearsAllCaches_WhenAllIsSpecified()
    {
        // Arrange
        var request = new CacheClearRequest { CacheTypes = new List<string> { "all" } };

        // Act
        var result = await _service.ClearCacheAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.ClearedCaches.ShouldContain("health");
        result.ClearedCaches.ShouldContain("query");
        result.ClearedCaches.ShouldContain("embedding");
        result.ClearedCaches.ShouldContain("token");
        result.ClearedAt.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task ClearCacheAsync_ClearsOnlySpecifiedCaches()
    {
        // Arrange
        var request = new CacheClearRequest { CacheTypes = new List<string> { "health" } };

        // Act
        var result = await _service.ClearCacheAsync(request);

        // Assert
        result.ClearedCaches.ShouldContain("health");
        result.ClearedCaches.ShouldNotContain("query");
    }

    [Fact]
    public async Task ClearCacheAsync_ClearsHealthCache_AndRemovesEntry()
    {
        // Arrange
        _memoryCache.Set("health_status", new HealthStatus
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0",
            Services = new Dictionary<string, ServiceHealth>()
        });

        var request = new CacheClearRequest { CacheTypes = new List<string> { "health" } };

        // Act
        var result = await _service.ClearCacheAsync(request);

        // Assert
        result.EntriesRemoved.ShouldBeGreaterThanOrEqualTo(1);
        _memoryCache.TryGetValue("health_status", out _).ShouldBeFalse();
    }

    #endregion
}
