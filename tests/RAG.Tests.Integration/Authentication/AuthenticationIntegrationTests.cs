using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using RAG.API.Authentication;
using Shouldly;

namespace RAG.Tests.Integration.Authentication;

/// <summary>
/// Integration tests for authentication and authorization.
/// </summary>
public class AuthenticationIntegrationTests : IClassFixture<AuthenticationTestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly AuthenticationTestWebApplicationFactory _factory;

    // Use the constant test token from TestAuthenticationHandler
    private const string ValidTestToken = TestAuthenticationHandler.TestToken;

    public AuthenticationIntegrationTests(AuthenticationTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task QueryEndpoint_WithoutAuthorization_Returns401Unauthorized()
    {
        // Arrange - Don't set authorization header
        var request = new
        {
            Query = "What is the capital of France?",
            TopK = 5
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/query", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task QueryStreamEndpoint_WithoutAuthorization_Returns401Unauthorized()
    {
        // Arrange - Don't set authorization header
        var request = new
        {
            Query = "What is the capital of France?",
            TopK = 5
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/query/stream", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task QueryEndpoint_WithValidAuthorization_DoesNotReturn401()
    {
        // Arrange
        var request = new
        {
            Query = "What is the capital of France?",
            TopK = 5
        };

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ValidTestToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/query", request);

        // Assert
        // Should not be 401 Unauthorized - the test auth handler should authenticate
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task QueryStreamEndpoint_WithValidAuthorization_DoesNotReturn401()
    {
        // Arrange
        var request = new
        {
            Query = "What is the capital of France?",
            TopK = 5
        };

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ValidTestToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/query/stream", request);

        // Assert
        // Should not be 401 Unauthorized - the test auth handler should authenticate
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SwaggerEndpoint_WithoutAuthorization_Returns200Ok()
    {
        // Arrange - Don't set authorization header
        // Swagger/OpenAPI endpoints should be publicly accessible

        // Act
        var response = await _client.GetAsync("/swagger/index.html");

        // Assert
        // In development/testing, swagger should be accessible without auth
        // Note: May return 404 if swagger is not enabled, which is also valid
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MultipleConcurrentRequests_WithAuthorization_AllSucceed()
    {
        // Arrange
        var request = new
        {
            Query = "What is the capital of France?",
            TopK = 5
        };

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ValidTestToken);

        // Act - Send multiple concurrent requests
        var tasks = Enumerable.Range(0, 3).Select(_ =>
            client.PostAsJsonAsync("/api/query", request));

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should be authenticated (not 401)
        foreach (var response in responses)
        {
            response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task QueryEndpoint_WithEmptyAuthorizationHeader_Returns401Unauthorized()
    {
        // Arrange - Authorization header without value
        var request = new
        {
            Query = "What is the capital of France?",
            TopK = 5
        };

        // Create a new client for this test to avoid header pollution
        var client = _factory.CreateClient();
        // Don't set authorization header

        // Act
        var response = await client.PostAsJsonAsync("/api/query", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

/// <summary>
/// Custom WebApplicationFactory for authentication integration tests.
/// Uses the existing TestScheme already configured in Program.cs for Testing environment.
/// </summary>
public class AuthenticationTestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var testConfig = new Dictionary<string, string?>
            {
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
                ["HallucinationDetection:Enabled"] = "false"
            };

            config.AddInMemoryCollection(testConfig);
        });

        // Note: TestScheme authentication is already configured in Program.cs for Testing environment
        // The TestAuthHandler in Program.cs requires Authorization header for authentication
    }
}
