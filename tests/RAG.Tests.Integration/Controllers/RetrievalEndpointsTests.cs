using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RAG.API.DTOs;
using Shouldly;

namespace RAG.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for retrieval API endpoints.
/// </summary>
public class RetrievalEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RetrievalEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        // Add test authentication token
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "dev-test-token-12345");
    }

    [Fact]
    public async Task PostBM25_WithValidRequest_Returns200()
    {
        // Arrange
        var request = new BM25RetrievalRequest("test query", TopK: 5);

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/bm25", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RetrievalResponse>();
        result.ShouldNotBeNull();
        result.Results.Count.ShouldBeLessThanOrEqualTo(5);
        result.Strategy.ShouldBe("BM25");
        result.TotalFound.ShouldBeGreaterThanOrEqualTo(0);
        result.RetrievalTimeMs.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task PostBM25_WithEmptyQuery_Returns400()
    {
        // Arrange
        var request = new BM25RetrievalRequest("", TopK: 5);

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/bm25", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostBM25_WithTopKZero_Returns400()
    {
        // Arrange
        var request = new BM25RetrievalRequest("test query", TopK: 0);

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/bm25", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostBM25_WithTopKExceedingMax_Returns400()
    {
        // Arrange
        var request = new BM25RetrievalRequest("test query", TopK: 101);

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/bm25", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostBM25_WithoutAuthorization_Returns401()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateClient();
        var request = new BM25RetrievalRequest("test query", TopK: 5);

        // Act
        var response = await unauthorizedClient.PostAsJsonAsync("/api/retrieval/bm25", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostBM25_WithDefaultTopK_UsesSettingsDefault()
    {
        // Arrange
        var request = new BM25RetrievalRequest("test query", TopK: null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/bm25", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RetrievalResponse>();
        result.ShouldNotBeNull();
        result.Results.Count.ShouldBeLessThanOrEqualTo(10); // Default is 10
    }

    [Fact]
    public async Task PostBM25_ResponseSchema_IsCorrect()
    {
        // Arrange
        var request = new BM25RetrievalRequest("test query", TopK: 5);

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/bm25", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RetrievalResponse>();
        result.ShouldNotBeNull();
        result.Results.ShouldNotBeNull();
        result.TotalFound.ShouldBeOfType<int>();
        result.RetrievalTimeMs.ShouldBeOfType<double>();
        result.Strategy.ShouldBeOfType<string>();
    }

    [Fact]
    public async Task PostDense_WithValidRequest_Returns200()
    {
        // Arrange
        var request = new DenseRetrievalRequest("test query", TopK: 5, Threshold: null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/dense", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RetrievalResponse>();
        result.ShouldNotBeNull();
        result.Results.Count.ShouldBeLessThanOrEqualTo(5);
        result.Strategy.ShouldBe("Dense");
        result.TotalFound.ShouldBeGreaterThanOrEqualTo(0);
        result.RetrievalTimeMs.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task PostDense_WithEmptyQuery_Returns400()
    {
        // Arrange
        var request = new DenseRetrievalRequest("", TopK: 5, Threshold: null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/dense", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostDense_WithTopKZero_Returns400()
    {
        // Arrange
        var request = new DenseRetrievalRequest("test query", TopK: 0, Threshold: null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/dense", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostDense_WithTopKExceedingMax_Returns400()
    {
        // Arrange
        var request = new DenseRetrievalRequest("test query", TopK: 101, Threshold: null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/dense", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostDense_WithInvalidThreshold_Returns400()
    {
        // Arrange
        var request = new DenseRetrievalRequest("test query", TopK: 5, Threshold: 1.5);

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/dense", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostDense_WithoutAuthorization_Returns401()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateClient();
        var request = new DenseRetrievalRequest("test query", TopK: 5, Threshold: null);

        // Act
        var response = await unauthorizedClient.PostAsJsonAsync("/api/retrieval/dense", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostDense_ResponseSchema_IsCorrect()
    {
        // Arrange
        var request = new DenseRetrievalRequest("test query", TopK: 5, Threshold: null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/dense", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RetrievalResponse>();
        result.ShouldNotBeNull();
        result.Results.ShouldNotBeNull();
        result.TotalFound.ShouldBeOfType<int>();
        result.RetrievalTimeMs.ShouldBeOfType<double>();
        result.Strategy.ShouldBeOfType<string>();
        result.Strategy.ShouldBe("Dense");
    }

    [Fact]
    public async Task PostDense_HighlightedText_IsAlwaysNull()
    {
        // Arrange
        var request = new DenseRetrievalRequest("test query", TopK: 5, Threshold: null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/retrieval/dense", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RetrievalResponse>();
        result.ShouldNotBeNull();

        // Dense retrieval does not provide highlighting
        foreach (var resultItem in result.Results)
        {
            resultItem.HighlightedText.ShouldBeNull();
        }
    }
}
