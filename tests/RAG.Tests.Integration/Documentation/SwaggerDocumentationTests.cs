using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace RAG.Tests.Integration.Documentation;

/// <summary>
/// Custom factory for Swagger documentation tests that enables Swagger in test environment.
/// </summary>
public class SwaggerTestWebApplicationFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // First call base configuration
        base.ConfigureWebHost(builder);

        // Override to use Development environment so Swagger is enabled
        builder.UseEnvironment("Development");
    }
}

/// <summary>
/// Integration tests for Swagger/OpenAPI documentation.
/// Validates that the API documentation is complete, accurate, and follows best practices.
/// </summary>
public class SwaggerDocumentationTests : IClassFixture<SwaggerTestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly SwaggerTestWebApplicationFactory _factory;

    public SwaggerDocumentationTests(SwaggerTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    /// <summary>
    /// Test: Swagger JSON endpoint returns valid OpenAPI spec (AC 3, 7).
    /// </summary>
    [Fact]
    public async Task SwaggerJson_ReturnsValidOpenApiSpec()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldNotBeNullOrWhiteSpace();

        // Parse as JSON to validate format
        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        // Verify OpenAPI version
        root.TryGetProperty("openapi", out var openApiVersion).ShouldBeTrue();
        openApiVersion.GetString().ShouldStartWith("3.");

        // Verify info section exists
        root.TryGetProperty("info", out var info).ShouldBeTrue();
        info.TryGetProperty("title", out var title).ShouldBeTrue();
        title.GetString().ShouldBe("RAG Architecture API");
        info.TryGetProperty("version", out var version).ShouldBeTrue();
        version.GetString().ShouldBe("v1");
    }

    /// <summary>
    /// Test: All endpoints have descriptions in OpenAPI spec (AC 4).
    /// </summary>
    [Fact]
    public async Task SwaggerJson_AllEndpointsHaveDescriptions()
    {
        // Arrange
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        // Act
        root.TryGetProperty("paths", out var paths).ShouldBeTrue();

        // Assert - check that each path has at least one operation with summary
        foreach (var path in paths.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                // Skip parameters and other non-operation properties
                if (operation.Name is "parameters" or "servers" or "summary" or "description")
                    continue;

                var hasDescription = operation.Value.TryGetProperty("summary", out var summary) ||
                                    operation.Value.TryGetProperty("description", out _);

                hasDescription.ShouldBeTrue(
                    $"Endpoint {operation.Name.ToUpper()} {path.Name} should have a summary or description");
            }
        }
    }

    /// <summary>
    /// Test: Authentication scheme is documented (AC 3, 6).
    /// </summary>
    [Fact]
    public async Task SwaggerJson_AuthenticationSchemeIsDocumented()
    {
        // Arrange
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        // Act & Assert
        root.TryGetProperty("components", out var components).ShouldBeTrue();
        components.TryGetProperty("securitySchemes", out var securitySchemes).ShouldBeTrue();
        securitySchemes.TryGetProperty("Bearer", out var bearerScheme).ShouldBeTrue();

        // Verify Bearer scheme details
        bearerScheme.TryGetProperty("type", out var type).ShouldBeTrue();
        type.GetString().ShouldBe("http");

        bearerScheme.TryGetProperty("scheme", out var scheme).ShouldBeTrue();
        scheme.GetString().ShouldBe("bearer");

        bearerScheme.TryGetProperty("bearerFormat", out var bearerFormat).ShouldBeTrue();
        bearerFormat.GetString().ShouldBe("JWT");
    }

    /// <summary>
    /// Test: All request/response types have schemas (AC 3).
    /// </summary>
    [Fact]
    public async Task SwaggerJson_AllTypesHaveSchemas()
    {
        // Arrange
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        // Act
        root.TryGetProperty("components", out var components).ShouldBeTrue();
        components.TryGetProperty("schemas", out var schemas).ShouldBeTrue();

        // Assert - verify key schemas exist
        // Note: Some schemas may have different names due to namespacing or how Swagger generates them
        var expectedSchemas = new[]
        {
            "QueryRequest",
            "QueryResponse",
            "BM25RetrievalRequest",
            "DenseRetrievalRequest",
            "HybridRetrievalRequest",
            "HybridRetrievalResponse",
            "RetrievalResponse"
        };

        foreach (var expectedSchema in expectedSchemas)
        {
            schemas.TryGetProperty(expectedSchema, out _).ShouldBeTrue(
                $"Schema '{expectedSchema}' should be documented");
        }
    }

    /// <summary>
    /// Test: OpenAPI spec contains operation tags for grouping (AC 3).
    /// </summary>
    [Fact]
    public async Task SwaggerJson_HasOperationTags()
    {
        // Arrange
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        // Act & Assert
        root.TryGetProperty("tags", out var tags).ShouldBeTrue();
        tags.GetArrayLength().ShouldBeGreaterThan(0);

        // Verify expected tags exist
        var tagNames = new List<string>();
        foreach (var tag in tags.EnumerateArray())
        {
            if (tag.TryGetProperty("name", out var name))
            {
                tagNames.Add(name.GetString() ?? "");
            }
        }

        tagNames.ShouldContain("Query");
        tagNames.ShouldContain("Documents");
        tagNames.ShouldContain("Retrieval");
        tagNames.ShouldContain("Health");
    }

    /// <summary>
    /// Test: OpenAPI spec includes response types for operations (AC 3).
    /// </summary>
    [Fact]
    public async Task SwaggerJson_OperationsHaveResponseTypes()
    {
        // Arrange
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        // Act
        root.TryGetProperty("paths", out var paths).ShouldBeTrue();

        // Assert - check that POST operations have response definitions
        var postOperationCount = 0;
        var operationsWithResponses = 0;

        foreach (var path in paths.EnumerateObject())
        {
            if (path.Value.TryGetProperty("post", out var postOp))
            {
                postOperationCount++;
                if (postOp.TryGetProperty("responses", out var responses))
                {
                    responses.EnumerateObject().Count().ShouldBeGreaterThan(0);
                    operationsWithResponses++;
                }
            }
        }

        operationsWithResponses.ShouldBe(postOperationCount,
            "All POST operations should have response definitions");
    }

    /// <summary>
    /// Test: API info section contains required metadata (AC 3).
    /// </summary>
    [Fact]
    public async Task SwaggerJson_InfoSectionIsComplete()
    {
        // Arrange
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        // Act
        root.TryGetProperty("info", out var info).ShouldBeTrue();

        // Assert
        info.TryGetProperty("title", out _).ShouldBeTrue();
        info.TryGetProperty("version", out _).ShouldBeTrue();
        info.TryGetProperty("description", out var description).ShouldBeTrue();
        description.GetString()!.Length.ShouldBeGreaterThan(100,
            "API description should be comprehensive");

        // Optional but recommended
        info.TryGetProperty("contact", out var contact).ShouldBeTrue();
        contact.TryGetProperty("name", out _).ShouldBeTrue();
    }

    /// <summary>
    /// Test: Security requirements are applied to protected endpoints (AC 6).
    /// </summary>
    [Fact]
    public async Task SwaggerJson_SecurityRequirementsApplied()
    {
        // Arrange
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        // Act & Assert - check global security requirement
        root.TryGetProperty("security", out var security).ShouldBeTrue(
            "Global security requirement should be defined");
        security.GetArrayLength().ShouldBeGreaterThan(0);
    }
}
