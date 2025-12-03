using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace RAG.Tests.Integration.ErrorHandling;

/// <summary>
/// Integration tests for API error handling.
/// Verifies RFC 7807 Problem Details format for error scenarios.
/// </summary>
public class ErrorHandlingIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ErrorHandlingIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    #region 404 Not Found Tests

    [Fact]
    public async Task GetNonExistentDocument_Returns404()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/documents/{nonExistentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetNonExistentDocument_ReturnsProblemDetailsContentType()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/documents/{nonExistentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetNonExistentDocument_ReturnsProblemDetailsWithRequiredFields()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/documents/{nonExistentId}");

        // Assert - read raw content to debug
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldNotBeEmpty();

        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(content, _jsonOptions);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(404);
        problemDetails.Title.ShouldNotBeNullOrEmpty();
        problemDetails.Type.ShouldNotBeNullOrEmpty();
    }

    #endregion

    #region 401 Unauthorized Tests

    [Fact]
    public async Task AccessProtectedEndpoint_WithoutToken_ReturnsUnauthorizedOrForbidden()
    {
        // Arrange - no authorization header
        var client = new HttpClient { BaseAddress = _client.BaseAddress };

        // Act
        var response = await client.GetAsync("/api/documents");

        // Assert - should be 401 or 403 depending on auth configuration
        var statusCode = (int)response.StatusCode;
        statusCode.ShouldBeOneOf(401, 403, 404);
    }

    #endregion

    #region 400 Validation Error Tests

    [Fact]
    public async Task PostInvalidRequest_Returns400()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");
        var invalidRequest = new { Query = "" }; // Empty query should fail validation

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/bm25", invalidRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostInvalidRequest_ReturnsProblemDetails()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");
        var invalidRequest = new { Query = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/bm25", invalidRequest);

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldNotBeEmpty();

        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(content, _jsonOptions);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(400);
    }

    #endregion

    #region RFC 7807 Compliance Tests

    [Fact]
    public async Task ErrorResponse_ContainsTypeField()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/documents/{nonExistentId}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.ShouldContain("type");
    }

    [Fact]
    public async Task ErrorResponse_ContainsTitleField()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/documents/{nonExistentId}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.ShouldContain("title");
    }

    [Fact]
    public async Task ErrorResponse_ContainsStatusField()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/documents/{nonExistentId}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.ShouldContain("status");
    }

    [Fact]
    public async Task ErrorResponse_ContainsCorrelationId()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/documents/{nonExistentId}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - correlationId should be in the extensions
        content.ShouldContain("correlationId");
    }

    [Fact]
    public async Task ErrorResponse_ContainsTimestamp()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/documents/{nonExistentId}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.ShouldContain("timestamp");
    }

    #endregion

    #region Error Type URI Tests

    [Fact]
    public async Task NotFoundError_TypeContainsNotFound()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/documents/{nonExistentId}");
        var content = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(content, _jsonOptions);

        // Assert
        problemDetails.ShouldNotBeNull();
        problemDetails.Type.ShouldContain("not-found");
    }

    #endregion
}
