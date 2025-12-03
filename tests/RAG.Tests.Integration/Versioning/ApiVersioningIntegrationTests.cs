using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RAG.API.DTOs;
using Shouldly;

namespace RAG.Tests.Integration.Versioning;

/// <summary>
/// Integration tests for API versioning functionality.
/// Verifies that endpoints respond correctly to versioned and unversioned requests.
/// </summary>
public class ApiVersioningIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiVersioningIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "dev-test-token-12345");
    }

    #region Versioned Routes (api/v1/*)

    [Fact]
    public async Task GetDocuments_VersionedRoute_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/documents");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostRetrieval_VersionedRoute_WithInvalidRequest_Returns400()
    {
        // Arrange
        var request = new BM25RetrievalRequest("", TopK: 5);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/retrieval/bm25", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostHybrid_VersionedRoute_WithInvalidRequest_Returns400()
    {
        // Arrange
        var request = new HybridRetrievalRequest("", TopK: null, Alpha: null, Beta: null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/retrieval/hybrid", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Backward Compatible Routes (api/*)

    [Fact]
    public async Task GetDocuments_BackwardCompatibleRoute_ReturnsSuccess()
    {
        // Act
        var response = await _client.GetAsync("/api/documents");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostRetrieval_BackwardCompatibleRoute_WithInvalidRequest_Returns400()
    {
        // Arrange
        var request = new BM25RetrievalRequest("", TopK: 5);

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/bm25", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostHybrid_BackwardCompatibleRoute_WithInvalidRequest_Returns400()
    {
        // Arrange
        var request = new HybridRetrievalRequest("", TopK: null, Alpha: null, Beta: null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/hybrid", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region API Version Headers

    [Fact]
    public async Task GetDocuments_ReturnsApiVersionHeader()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/documents");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.TryGetValues("api-supported-versions", out var versions).ShouldBeTrue();
        versions.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetDocuments_BackwardCompatible_ReturnsApiVersionHeader()
    {
        // Act
        var response = await _client.GetAsync("/api/documents");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.TryGetValues("api-supported-versions", out var versions).ShouldBeTrue();
        versions.ShouldNotBeEmpty();
    }

    #endregion

    #region Both Routes Return Same Response Structure

    [Fact]
    public async Task GetDocuments_BothRoutes_ReturnSameResponseStructure()
    {
        // Act
        var versionedResponse = await _client.GetAsync("/api/v1/documents");
        var backwardCompatibleResponse = await _client.GetAsync("/api/documents");

        // Assert
        versionedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        backwardCompatibleResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var versionedContent = await versionedResponse.Content.ReadAsStringAsync();
        var backwardCompatibleContent = await backwardCompatibleResponse.Content.ReadAsStringAsync();

        // Both should return JSON with similar structure (may have different data)
        versionedContent.ShouldContain("items");
        backwardCompatibleContent.ShouldContain("items");
    }

    #endregion

    #region Authentication Consistent Across Versions

    [Fact]
    public async Task GetDocuments_VersionedRoute_WithoutAuth_Returns401()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/api/v1/documents");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDocuments_BackwardCompatibleRoute_WithoutAuth_Returns401()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/api/documents");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Query Endpoint Versioning

    [Fact]
    public async Task PostQuery_VersionedRoute_WithInvalidRequest_Returns400()
    {
        // Arrange
        var request = new QueryRequest { Query = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/query", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostQuery_BackwardCompatibleRoute_WithInvalidRequest_Returns400()
    {
        // Arrange
        var request = new QueryRequest { Query = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/query", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Dense Retrieval Versioning

    [Fact]
    public async Task PostDenseRetrieval_VersionedRoute_WithInvalidRequest_Returns400()
    {
        // Arrange
        var request = new DenseRetrievalRequest("", TopK: 5, Threshold: null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/retrieval/dense", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostDenseRetrieval_BackwardCompatibleRoute_WithInvalidRequest_Returns400()
    {
        // Arrange
        var request = new DenseRetrievalRequest("", TopK: 5, Threshold: null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/dense", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion
}
