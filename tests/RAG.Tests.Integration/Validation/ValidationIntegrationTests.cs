using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace RAG.Tests.Integration.Validation;

/// <summary>
/// Integration tests for API request validation.
/// Tests that validation middleware returns proper RFC 7807 Problem Details responses.
/// </summary>
public class ValidationIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ValidationIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("Authorization", "Bearer user-token");
    }

    [Fact]
    public async Task QueryEndpoint_EmptyQuery_Returns400WithValidationErrors()
    {
        // Arrange
        var request = new { Query = "", TopK = 10 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/query", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        // Content type can be application/json or application/problem+json
        response.Content.Headers.ContentType?.MediaType.ShouldNotBeNull();
        response.Content.Headers.ContentType!.MediaType.ShouldContain("json");

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(400);
        problemDetails.Title.ShouldBe("Validation Failed");
        problemDetails.Type.ShouldNotBeNull();
        problemDetails.Type.ShouldContain("validation");

        // Verify errors extension contains the query error
        problemDetails.Extensions.ShouldContainKey("errors");
    }

    [Fact]
    public async Task QueryEndpoint_InvalidTopK_Returns400WithValidationErrors()
    {
        // Arrange
        var request = new { Query = "test query", TopK = 0 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/query", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(400);
        problemDetails.Extensions.ShouldContainKey("errors");
    }

    [Fact]
    public async Task QueryEndpoint_TopKAboveMax_Returns400WithValidationErrors()
    {
        // Arrange
        var request = new { Query = "test query", TopK = 101 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/query", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(400);
    }

    [Fact]
    public async Task QueryEndpoint_InvalidStrategy_Returns400WithValidationErrors()
    {
        // Arrange
        var request = new { Query = "test query", Strategy = "InvalidStrategy" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/query", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(400);
    }

    [Fact]
    public async Task QueryEndpoint_InvalidProvider_Returns400WithValidationErrors()
    {
        // Arrange
        var request = new { Query = "test query", Provider = "InvalidProvider" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/query", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(400);
    }

    [Fact]
    public async Task QueryEndpoint_TemperatureOutOfRange_Returns400WithValidationErrors()
    {
        // Arrange
        var request = new { Query = "test query", Temperature = 1.5 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/query", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(400);
    }

    [Fact]
    public async Task QueryEndpoint_MaxTokensOutOfRange_Returns400WithValidationErrors()
    {
        // Arrange
        var request = new { Query = "test query", MaxTokens = 10000 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/query", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(400);
    }

    [Fact]
    public async Task HybridSearchEndpoint_EmptyQuery_Returns400WithValidationErrors()
    {
        // Arrange
        var request = new { Query = "", TopK = 10 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/hybrid", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(400);
    }

    [Fact]
    public async Task HybridSearchEndpoint_InvalidWeights_Returns400WithValidationErrors()
    {
        // Arrange - Alpha + Beta != 1.0
        var request = new { Query = "test", Alpha = 0.6, Beta = 0.6 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/hybrid", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problemDetails.ShouldNotBeNull();
        problemDetails.Status.ShouldBe(400);
    }

    [Fact]
    public async Task ValidationResponse_IncludesCorrelationId()
    {
        // Arrange
        var request = new { Query = "" };
        var correlationId = Guid.NewGuid().ToString("N")[..12];
        _client.DefaultRequestHeaders.Remove("X-Correlation-ID");
        _client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/query", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problemDetails.ShouldNotBeNull();
        problemDetails.Extensions.ShouldContainKey("correlationId");
        problemDetails.Extensions["correlationId"]?.ToString().ShouldBe(correlationId);
    }

    [Fact]
    public async Task ValidationResponse_IncludesTimestamp()
    {
        // Arrange
        var request = new { Query = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/query", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problemDetails.ShouldNotBeNull();
        problemDetails.Extensions.ShouldContainKey("timestamp");
    }

    [Fact]
    public async Task ValidationResponse_ErrorsGroupedByPropertyName()
    {
        // Arrange - Multiple validation errors
        var request = new { Query = "", TopK = 0 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/query", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        // Verify errors are structured as expected
        root.TryGetProperty("errors", out var errors).ShouldBeTrue();
        errors.ValueKind.ShouldBe(JsonValueKind.Object);

        // Should have at least the query error
        errors.TryGetProperty("query", out var queryErrors).ShouldBeTrue();
        queryErrors.ValueKind.ShouldBe(JsonValueKind.Array);
    }
}
