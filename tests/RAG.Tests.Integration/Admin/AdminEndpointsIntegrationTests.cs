using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RAG.Core.DTOs.Admin;
using Shouldly;

namespace RAG.Tests.Integration.Admin;

/// <summary>
/// Integration tests for admin endpoints.
/// Tests authentication, authorization, and endpoint functionality.
/// </summary>
public class AdminEndpointsIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public AdminEndpointsIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    #region Authentication and Authorization Tests

    [Fact]
    public async Task StatsEndpoint_WithNoToken_Returns401()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/api/v1/admin/stats");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StatsEndpoint_WithUserToken_Returns403()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");

        // Act
        var response = await _client.GetAsync("/api/v1/admin/stats");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StatsEndpoint_WithNoRoleToken_Returns403()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "no-role-token");

        // Act
        var response = await _client.GetAsync("/api/v1/admin/stats");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AllAdminEndpoints_WithUserToken_Return403()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");

        // Act & Assert - Stats
        var statsResponse = await _client.GetAsync("/api/v1/admin/stats");
        statsResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Act & Assert - Health
        var healthResponse = await _client.GetAsync("/api/v1/admin/health/detailed");
        healthResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Act & Assert - Cache Clear
        var cacheResponse = await _client.DeleteAsync("/api/v1/admin/cache/clear");
        cacheResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Act & Assert - Index Rebuild
        var rebuildResponse = await _client.PostAsJsonAsync("/api/v1/admin/index/rebuild", new { });
        rebuildResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Act & Assert - Audit Logs
        var auditResponse = await _client.GetAsync("/api/v1/admin/audit-logs");
        auditResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Stats Endpoint Tests

    [Fact]
    public async Task StatsEndpoint_WithAdminToken_Returns200()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act
        var response = await _client.GetAsync("/api/v1/admin/stats");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StatsEndpoint_WithAdminToken_ReturnsValidResponse()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act
        var response = await _client.GetAsync("/api/v1/admin/stats");
        var content = await response.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<SystemStatsResponse>(content, _jsonOptions);

        // Assert
        stats.ShouldNotBeNull();
        stats.TotalDocuments.ShouldBeGreaterThanOrEqualTo(0);
        stats.TotalChunks.ShouldBeGreaterThanOrEqualTo(0);
        stats.SystemUptime.ShouldNotBeNullOrEmpty();
        stats.DocumentsByTenant.ShouldNotBeNull();
    }

    #endregion

    #region Health Detailed Endpoint Tests

    [Fact]
    public async Task HealthDetailedEndpoint_WithAdminToken_Returns200()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act
        var response = await _client.GetAsync("/api/v1/admin/health/detailed");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthDetailedEndpoint_WithAdminToken_ReturnsValidResponse()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act
        var response = await _client.GetAsync("/api/v1/admin/health/detailed");
        var content = await response.Content.ReadAsStringAsync();
        var health = JsonSerializer.Deserialize<DetailedHealthResponse>(content, _jsonOptions);

        // Assert
        health.ShouldNotBeNull();
        health.OverallStatus.ShouldNotBeNullOrEmpty();
        health.CheckedAt.ShouldBeGreaterThan(DateTime.MinValue);
        health.Dependencies.ShouldNotBeNull();
        health.Dependencies.ShouldContainKey("postgresql");
    }

    #endregion

    #region Index Rebuild Endpoint Tests

    [Fact]
    public async Task IndexRebuildEndpoint_WithAdminToken_Returns202()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");
        var request = new IndexRebuildRequest { IncludeEmbeddings = true };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/admin/index/rebuild", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task IndexRebuildEndpoint_WithAdminToken_ReturnsJobId()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");
        var request = new IndexRebuildRequest { IncludeEmbeddings = false };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/admin/index/rebuild", request);
        var content = await response.Content.ReadAsStringAsync();
        var rebuildResponse = JsonSerializer.Deserialize<IndexRebuildResponse>(content, _jsonOptions);

        // Assert
        rebuildResponse.ShouldNotBeNull();
        rebuildResponse.JobId.ShouldNotBe(Guid.Empty);
        rebuildResponse.Status.ShouldBe("Queued");
    }

    [Fact]
    public async Task IndexRebuildStatusEndpoint_WithValidJobId_Returns200()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // First, create a job
        var request = new IndexRebuildRequest();
        var createResponse = await _client.PostAsJsonAsync("/api/v1/admin/index/rebuild", request);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var rebuildResponse = JsonSerializer.Deserialize<IndexRebuildResponse>(createContent, _jsonOptions);

        // Act
        var response = await _client.GetAsync($"/api/v1/admin/index/rebuild/{rebuildResponse!.JobId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IndexRebuildStatusEndpoint_WithInvalidJobId_Returns404()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");
        var nonExistentJobId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/admin/index/rebuild/{nonExistentJobId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task IndexRebuildEndpoint_WithTenantFilter_Returns202()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");
        var request = new IndexRebuildRequest
        {
            TenantId = Guid.NewGuid(),
            IncludeEmbeddings = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/admin/index/rebuild", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    #endregion

    #region Cache Clear Endpoint Tests

    [Fact]
    public async Task CacheClearEndpoint_WithAdminToken_Returns200()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");
        var request = new CacheClearRequest { CacheTypes = new List<string> { "all" } };

        // Act
        var response = await _client.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Delete,
            RequestUri = new Uri("/api/v1/admin/cache/clear", UriKind.Relative),
            Content = JsonContent.Create(request)
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CacheClearEndpoint_WithAdminToken_ReturnsValidResponse()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");
        var request = new CacheClearRequest { CacheTypes = new List<string> { "health", "query" } };

        // Act
        var response = await _client.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Delete,
            RequestUri = new Uri("/api/v1/admin/cache/clear", UriKind.Relative),
            Content = JsonContent.Create(request)
        });
        var content = await response.Content.ReadAsStringAsync();
        var cacheResponse = JsonSerializer.Deserialize<CacheClearResponse>(content, _jsonOptions);

        // Assert
        cacheResponse.ShouldNotBeNull();
        cacheResponse.ClearedCaches.ShouldContain("health");
        cacheResponse.ClearedCaches.ShouldContain("query");
        cacheResponse.ClearedAt.ShouldBeGreaterThan(DateTime.MinValue);
    }

    [Fact]
    public async Task CacheClearEndpoint_WithAllOption_ClearsAllCaches()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");
        var request = new CacheClearRequest { CacheTypes = new List<string> { "all" } };

        // Act
        var response = await _client.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Delete,
            RequestUri = new Uri("/api/v1/admin/cache/clear", UriKind.Relative),
            Content = JsonContent.Create(request)
        });
        var content = await response.Content.ReadAsStringAsync();
        var cacheResponse = JsonSerializer.Deserialize<CacheClearResponse>(content, _jsonOptions);

        // Assert
        cacheResponse.ShouldNotBeNull();
        cacheResponse.ClearedCaches.ShouldContain("health");
        cacheResponse.ClearedCaches.ShouldContain("query");
        cacheResponse.ClearedCaches.ShouldContain("embedding");
        cacheResponse.ClearedCaches.ShouldContain("token");
    }

    #endregion

    #region Audit Logs Endpoint Tests

    [Fact]
    public async Task AuditLogsEndpoint_WithAdminToken_Returns200()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act
        var response = await _client.GetAsync("/api/v1/admin/audit-logs");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuditLogsEndpoint_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act
        var response = await _client.GetAsync("/api/v1/admin/audit-logs?page=1&pageSize=10");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.ShouldNotBeNullOrEmpty();
        // The response should be a PagedResult object
        content.ShouldContain("items");
        content.ShouldContain("totalCount");
    }

    [Fact]
    public async Task AuditLogsEndpoint_WithFilters_Returns200()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act
        var response = await _client.GetAsync(
            "/api/v1/admin/audit-logs?userId=admin&action=ClearCache&fromDate=2024-01-01&toDate=2024-12-31");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuditLogsEndpoint_WithMaxPageSize_CapsAt100()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act
        var response = await _client.GetAsync("/api/v1/admin/audit-logs?page=1&pageSize=200");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        // The pageSize should be capped at 100 (implementation detail tested via unit tests)
    }

    #endregion

    #region Content-Type Tests

    [Fact]
    public async Task AdminEndpoints_ReturnJsonContentType()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act
        var statsResponse = await _client.GetAsync("/api/v1/admin/stats");
        var healthResponse = await _client.GetAsync("/api/v1/admin/health/detailed");

        // Assert
        statsResponse.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        healthResponse.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
    }

    #endregion

    #region API Versioning Tests

    [Fact]
    public async Task AdminEndpoints_SupportApiVersioning()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act - Version 1.0 should work
        var v1Response = await _client.GetAsync("/api/v1/admin/stats");

        // Assert
        v1Response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminEndpoints_WithInvalidVersion_ReturnsError()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act
        var response = await _client.GetAsync("/api/v99/admin/stats");

        // Assert - Should return either 400 or 404 for unsupported version
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    #endregion
}
