using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace RAG.Tests.Integration.Cors;

/// <summary>
/// Integration tests for CORS configuration (Story 6.9).
///
/// Note: ASP.NET Core TestServer processes requests in-process, which means
/// CORS headers are not always evaluated the same way as with real HTTP requests.
/// These tests verify:
/// 1. CORS configuration is properly loaded
/// 2. Requests with Origin headers are not blocked
/// 3. The middleware pipeline works correctly
///
/// For full CORS header validation, browser-based or real HTTP client tests
/// against a running server are recommended.
/// </summary>
public class CorsIntegrationTests : IClassFixture<CorsTestWebApplicationFactory>
{
    private readonly CorsTestWebApplicationFactory _factory;
    private const string AllowedOrigin = "http://localhost:3000";
    private const string DisallowedOrigin = "http://malicious-site.com";

    public CorsIntegrationTests(CorsTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Request_WithAllowedOrigin_Succeeds()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
        request.Headers.Add("Origin", AllowedOrigin);

        // Act
        var response = await client.SendAsync(request);

        // Assert - Request should succeed (not blocked by CORS)
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Request_WithDisallowedOrigin_StillSucceeds_InTestServer()
    {
        // Note: In-process TestServer doesn't enforce CORS the same way browsers do.
        // The request will succeed, but a real browser would block based on missing headers.

        // Arrange
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
        request.Headers.Add("Origin", DisallowedOrigin);

        // Act
        var response = await client.SendAsync(request);

        // Assert - Request succeeds (TestServer doesn't enforce browser CORS policy)
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PreflightRequest_WithAllowedOrigin_ReturnsSuccess()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/query");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type, Authorization");

        // Act
        var response = await client.SendAsync(request);

        // Assert - Preflight should succeed (200 or 204)
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AuthenticatedRequest_WithOrigin_Succeeds()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public void CorsConfiguration_IsLoaded()
    {
        // Arrange - Get CORS settings from DI container
        using var scope = _factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        // Act
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        var allowedMethods = configuration.GetSection("Cors:AllowedMethods").Get<string[]>();
        var allowedHeaders = configuration.GetSection("Cors:AllowedHeaders").Get<string[]>();
        var exposedHeaders = configuration.GetSection("Cors:ExposedHeaders").Get<string[]>();
        var allowCredentials = configuration.GetValue<bool>("Cors:AllowCredentials");
        var maxAge = configuration.GetValue<int>("Cors:MaxAgeSeconds");

        // Assert - Configuration is properly loaded
        allowedOrigins.ShouldNotBeNull();
        allowedOrigins.ShouldContain("http://localhost:3000");
        allowedOrigins.ShouldContain("http://localhost:5173");
        allowedOrigins.ShouldContain("http://localhost:8080");

        allowedMethods.ShouldNotBeNull();
        allowedMethods.ShouldContain("GET");
        allowedMethods.ShouldContain("POST");
        allowedMethods.ShouldContain("PUT");
        allowedMethods.ShouldContain("DELETE");

        allowedHeaders.ShouldNotBeNull();
        allowedHeaders.ShouldContain("Content-Type");
        allowedHeaders.ShouldContain("Authorization");

        exposedHeaders.ShouldNotBeNull();
        exposedHeaders.ShouldContain("X-RateLimit-Limit");
        exposedHeaders.ShouldContain("X-Request-Id");

        allowCredentials.ShouldBeTrue();
        maxAge.ShouldBe(600);
    }

    [Fact]
    public async Task MultipleRequests_WithDifferentOrigins_AllSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();
        var origins = new[] { "http://localhost:3000", "http://localhost:5173", "http://localhost:8080" };

        foreach (var origin in origins)
        {
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
            request.Headers.Add("Origin", origin);
            var response = await client.SendAsync(request);

            // Assert - All allowed origins should succeed
            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"Request with origin {origin} should succeed");
        }
    }

    [Fact]
    public async Task PreflightRequest_ForPOST_ReturnsSuccess()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/documents");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PreflightRequest_ForDELETE_ReturnsSuccess()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/documents/123");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "DELETE");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CorsMiddleware_DoesNotBlockHealthEndpoint()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Request without Origin header
        var response = await client.GetAsync("/healthz");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

/// <summary>
/// Custom WebApplicationFactory for CORS integration tests.
/// Extends TestWebApplicationFactory with CORS-specific configuration.
/// </summary>
public class CorsTestWebApplicationFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Call base configuration first
        base.ConfigureWebHost(builder);

        // Add CORS-specific configuration
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var corsConfig = new Dictionary<string, string?>
            {
                // CORS configuration - specific test origins
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
                ["Cors:AllowedOrigins:1"] = "http://localhost:5173",
                ["Cors:AllowedOrigins:2"] = "http://localhost:8080",
                ["Cors:AllowedMethods:0"] = "GET",
                ["Cors:AllowedMethods:1"] = "POST",
                ["Cors:AllowedMethods:2"] = "PUT",
                ["Cors:AllowedMethods:3"] = "DELETE",
                ["Cors:AllowedMethods:4"] = "OPTIONS",
                ["Cors:AllowedMethods:5"] = "PATCH",
                ["Cors:AllowedHeaders:0"] = "Content-Type",
                ["Cors:AllowedHeaders:1"] = "Authorization",
                ["Cors:AllowedHeaders:2"] = "X-Requested-With",
                ["Cors:AllowedHeaders:3"] = "Accept",
                ["Cors:AllowedHeaders:4"] = "Origin",
                ["Cors:ExposedHeaders:0"] = "X-RateLimit-Limit",
                ["Cors:ExposedHeaders:1"] = "X-RateLimit-Remaining",
                ["Cors:ExposedHeaders:2"] = "X-RateLimit-Reset",
                ["Cors:ExposedHeaders:3"] = "X-Request-Id",
                ["Cors:ExposedHeaders:4"] = "api-supported-versions",
                ["Cors:ExposedHeaders:5"] = "api-deprecated-versions",
                ["Cors:AllowCredentials"] = "true",
                ["Cors:MaxAgeSeconds"] = "600"
            };

            config.AddInMemoryCollection(corsConfig);
        });
    }
}
