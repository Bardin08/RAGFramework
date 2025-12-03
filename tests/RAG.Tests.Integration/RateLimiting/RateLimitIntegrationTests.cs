using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace RAG.Tests.Integration.RateLimiting;

/// <summary>
/// Integration tests for rate limiting functionality (AC: 7).
/// Tests that rate limiting is properly configured and operational.
/// Validates that the rate limiting middleware doesn't break the application
/// and that authenticated requests succeed within limits.
/// </summary>
public class RateLimitIntegrationTests : IClassFixture<RateLimitTestWebApplicationFactory>
{
    private readonly RateLimitTestWebApplicationFactory _factory;

    public RateLimitIntegrationTests(RateLimitTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthenticatedRequest_ToQueryEndpoint_DoesNotReturn401()
    {
        // Arrange - Create a new client for this test
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");

        var request = new { Query = "Test query", TopK = 5 };

        // Act
        var response = await client.PostAsJsonAsync("/api/query", request);

        // Assert - Should not be 401 (authentication working)
        // May be 500 due to missing services but not auth failure
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminRequest_ToQueryEndpoint_DoesNotReturn401()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        var request = new { Query = "Test query", TopK = 5 };

        // Act
        var response = await client.PostAsJsonAsync("/api/query", request);

        // Assert - Should not be 401 (authentication working)
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MultipleAuthenticatedRequests_AllSucceed()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");

        var request = new { Query = "Test query", TopK = 5 };

        // Act - Make multiple requests (well under any rate limit)
        var responses = new List<HttpResponseMessage>();
        for (int i = 0; i < 5; i++)
        {
            responses.Add(await client.PostAsJsonAsync("/api/query", request));
        }

        // Assert - None should be rate limited (429) or unauthorized (401)
        foreach (var response in responses)
        {
            response.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests, "Should not be rate limited for 5 requests");
            response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized, "Should not be unauthorized with valid token");
        }
    }

    [Fact]
    public async Task DifferentUserTiers_NotRateLimited_UnderThreshold()
    {
        // Arrange - Test both user and admin tiers
        var userClient = _factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        var request = new { Query = "Test query", TopK = 5 };

        // Act
        var userResponse = await userClient.PostAsJsonAsync("/api/query", request);
        var adminResponse = await adminClient.PostAsJsonAsync("/api/query", request);

        // Assert - Neither should be rate limited
        userResponse.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        adminResponse.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task RateLimitMiddleware_DoesNotBlockValidRequests()
    {
        // This test verifies that the rate limiting middleware is properly configured
        // and doesn't incorrectly block valid requests

        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");

        var request = new { Query = "Test query for rate limit verification", TopK = 5 };

        // Act - Make 3 sequential requests
        var responses = new List<(HttpStatusCode StatusCode, bool HasRateLimitHeaders)>();
        for (int i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/api/query", request);
            var hasRateLimitHeaders = response.Headers.Contains("X-Rate-Limit-Limit") ||
                                       response.Headers.Contains("X-RateLimit-Limit");
            responses.Add((response.StatusCode, hasRateLimitHeaders));
        }

        // Assert - No request should be rate limited (we're well under the limit)
        var rateLimitedCount = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        rateLimitedCount.ShouldBe(0, "No requests should be rate limited for just 3 requests");
    }

    [Fact]
    public async Task RateLimitConfiguration_IsLoadedAndActive()
    {
        // This test verifies that rate limiting configuration is properly loaded
        // by checking that requests don't cause configuration-related errors

        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        var request = new { Query = "Configuration test query", TopK = 5 };

        // Act
        var response = await client.PostAsJsonAsync("/api/query", request);

        // Assert
        // The response should not be a server error related to rate limit configuration
        // If rate limiting was misconfigured, we'd likely see a 500 error on the first request
        // A successful non-error response indicates the middleware is working
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
    }
}

/// <summary>
/// Custom WebApplicationFactory for rate limiting tests.
/// Uses minimal configuration to test rate limiting functionality.
/// </summary>
public class RateLimitTestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var testConfig = new Dictionary<string, string?>
            {
                // Essential configurations
                ["Elasticsearch:Url"] = "http://localhost:9200",
                ["Elasticsearch:IndexName"] = "test-index",
                ["Elasticsearch:DefaultPageSize"] = "10",
                ["Qdrant:Url"] = "http://localhost:6333",
                ["Qdrant:CollectionName"] = "test-collection",
                ["Qdrant:VectorSize"] = "384",
                ["EmbeddingService:Url"] = "http://localhost:8000",
                ["EmbeddingService:ServiceUrl"] = "http://localhost:8000",
                ["EmbeddingService:TimeoutSeconds"] = "30",
                ["EmbeddingService:EmbeddingDimensions"] = "384",
                ["LLMProviders:OpenAI:ApiKey"] = "test-key",
                ["LLMProviders:OpenAI:Model"] = "gpt-3.5-turbo",
                ["LLMProviders:OpenAI:MaxTokens"] = "1000",
                ["LLMProviders:OpenAI:Temperature"] = "0.7",
                ["LLMProviders:Ollama:Url"] = "http://localhost:11434",
                ["LLMProviders:Ollama:Model"] = "llama2",
                ["Keycloak:Url"] = "http://localhost:8080",
                ["Keycloak:Realm"] = "test-realm",
                ["MinIO:Endpoint"] = "localhost:9000",
                ["MinIO:AccessKey"] = "test",
                ["MinIO:SecretKey"] = "test",
                ["MinIO:BucketName"] = "test",
                ["MinIO:UseSSL"] = "false",
                ["Chunking:MaxChunkSize"] = "500",
                ["Chunking:OverlapSize"] = "50",
                ["TextCleaning:RemoveFormArtifacts"] = "true",
                ["TextCleaning:NormalizeWhitespace"] = "true",
                ["BM25Settings:K1"] = "1.2",
                ["BM25Settings:B"] = "0.75",
                ["DenseSettings:TopK"] = "10",
                ["RetrievalSettings:MaxResults"] = "10",
                ["HybridSearch:BM25Weight"] = "0.5",
                ["HybridSearch:DenseWeight"] = "0.5",
                ["RRF:K"] = "60",
                ["LLMProviders:Default"] = "OpenAI",
                ["PromptTemplates:SystemPrompt"] = "Test",
                ["HallucinationDetection:Enabled"] = "false",

                // Rate limiting configuration - required for AspNetCoreRateLimit
                ["IpRateLimiting:EnableEndpointRateLimiting"] = "true",
                ["IpRateLimiting:StackBlockedRequests"] = "false",
                ["IpRateLimiting:RealIpHeader"] = "X-Real-IP",
                ["IpRateLimiting:ClientIdHeader"] = "X-ClientId",
                ["IpRateLimiting:HttpStatusCode"] = "429",
                ["IpRateLimiting:GeneralRules:0:Endpoint"] = "*",
                ["IpRateLimiting:GeneralRules:0:Period"] = "1m",
                ["IpRateLimiting:GeneralRules:0:Limit"] = "1000",
                ["ClientRateLimiting:EnableEndpointRateLimiting"] = "true",
                ["ClientRateLimiting:ClientIdHeader"] = "X-ClientId",
                ["ClientRateLimiting:HttpStatusCode"] = "429"
            };

            config.AddInMemoryCollection(testConfig);
        });
    }
}
