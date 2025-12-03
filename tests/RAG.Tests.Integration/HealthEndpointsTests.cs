using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RAG.Core.Domain;
using Shouldly;

namespace RAG.Tests.Integration;

/// <summary>
/// Integration tests for health check endpoints.
/// </summary>
public class HealthEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Healthz_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/healthz");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldBe("OK");
    }

    [Fact]
    public async Task HealthzLive_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/healthz/live");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldBe("OK");
    }

    [Fact]
    public async Task HealthzReady_ReturnsStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/healthz/ready");

        // Assert
        // Note: This test may return either 200 or 503 depending on whether services are running
        // In a real environment, we would use TestContainers to ensure services are available
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task AdminHealth_ReturnsDetailedJson()
    {
        // Arrange - admin-token provides admin role required for /api/admin/health
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act
        var response = await _client.GetAsync("/api/admin/health");

        // Assert
        // Note: May return 503 if services are not running, but should still return JSON
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        var healthStatus = await response.Content.ReadFromJsonAsync<HealthStatus>();
        healthStatus.ShouldNotBeNull();
        healthStatus.Status.ShouldNotBeNullOrEmpty();
        healthStatus.Version.ShouldNotBeNullOrEmpty();
        healthStatus.Timestamp.ShouldNotBe(default(DateTime));
        healthStatus.Services.ShouldNotBeNull();
        healthStatus.Services.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task AdminHealth_ContainsExpectedServices()
    {
        // Arrange - admin-token provides admin role required for /api/admin/health
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act
        var response = await _client.GetAsync("/api/admin/health");
        var healthStatus = await response.Content.ReadFromJsonAsync<HealthStatus>();

        // Assert
        healthStatus.ShouldNotBeNull();
        healthStatus.Services.ShouldContainKey("elasticsearch");
        healthStatus.Services.ShouldContainKey("qdrant");
        healthStatus.Services.ShouldContainKey("keycloak");
        healthStatus.Services.ShouldContainKey("embeddingService");
    }

    [Fact]
    public async Task AdminHealth_CachingWorks()
    {
        // Arrange - admin-token provides admin role required for /api/admin/health
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act - Make two requests within cache duration (10 seconds)
        var response1 = await _client.GetAsync("/api/admin/health");
        var healthStatus1 = await response1.Content.ReadFromJsonAsync<HealthStatus>();

        var response2 = await _client.GetAsync("/api/admin/health");
        var healthStatus2 = await response2.Content.ReadFromJsonAsync<HealthStatus>();

        // Assert - Timestamps should be identical due to caching
        healthStatus1.ShouldNotBeNull();
        healthStatus2.ShouldNotBeNull();
        healthStatus1.Timestamp.ShouldBe(healthStatus2.Timestamp);
    }
}
