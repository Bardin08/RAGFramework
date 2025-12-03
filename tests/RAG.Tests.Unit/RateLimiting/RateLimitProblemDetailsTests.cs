using RAG.Infrastructure.RateLimiting;
using Shouldly;

namespace RAG.Tests.Unit.RateLimiting;

/// <summary>
/// Unit tests for RateLimitProblemDetails response format (AC: 4).
/// Tests that 429 response format matches RFC 7807.
/// </summary>
public class RateLimitProblemDetailsTests
{
    [Fact]
    public void RateLimitProblemDetails_CanBeInstantiated()
    {
        // Arrange & Act
        var problemDetails = new RateLimitProblemDetails();

        // Assert
        problemDetails.ShouldNotBeNull();
    }

    [Fact]
    public void RateLimitProblemDetails_CanSetAllProperties()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var problemDetails = new RateLimitProblemDetails
        {
            Type = "https://api.rag.system/errors/rate-limit-exceeded",
            Title = "Rate limit exceeded",
            Status = 429,
            Detail = "You have exceeded the rate limit. Try again in 60 seconds.",
            Instance = "/api/v1/query",
            RetryAfter = 60,
            CorrelationId = "abc123def456",
            Timestamp = timestamp
        };

        // Assert
        problemDetails.Type.ShouldBe("https://api.rag.system/errors/rate-limit-exceeded");
        problemDetails.Title.ShouldBe("Rate limit exceeded");
        problemDetails.Status.ShouldBe(429);
        problemDetails.Detail.ShouldContain("60 seconds");
        problemDetails.Instance.ShouldBe("/api/v1/query");
        problemDetails.RetryAfter.ShouldBe(60);
        problemDetails.CorrelationId.ShouldBe("abc123def456");
        problemDetails.Timestamp.ShouldBe(timestamp);
    }

    [Fact]
    public void RateLimitProblemDetails_StatusIs429()
    {
        // Arrange & Act
        var problemDetails = new RateLimitProblemDetails
        {
            Status = 429
        };

        // Assert - AC 4: 429 Too Many Requests
        problemDetails.Status.ShouldBe(429);
    }

    [Fact]
    public void RateLimitProblemDetails_TypeIsValidUri()
    {
        // Arrange
        var problemDetails = new RateLimitProblemDetails
        {
            Type = "https://api.rag.system/errors/rate-limit-exceeded"
        };

        // Act
        var isValidUri = Uri.TryCreate(problemDetails.Type, UriKind.Absolute, out var uri);

        // Assert - RFC 7807 requires Type to be a valid URI
        isValidUri.ShouldBeTrue();
        uri!.Scheme.ShouldBe("https");
    }

    [Fact]
    public void RateLimitProblemDetails_RetryAfter_IsPositiveInteger()
    {
        // Arrange & Act
        var problemDetails = new RateLimitProblemDetails
        {
            RetryAfter = 60
        };

        // Assert
        problemDetails.RetryAfter.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void RateLimitProblemDetails_Serialization_MatchesExpectedFormat()
    {
        // Arrange
        var problemDetails = new RateLimitProblemDetails
        {
            Type = "https://api.rag.system/errors/rate-limit-exceeded",
            Title = "Rate limit exceeded",
            Status = 429,
            Detail = "You have exceeded the rate limit. Try again in 60 seconds.",
            Instance = "/api/v1/query",
            RetryAfter = 60,
            CorrelationId = "abc123",
            Timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero)
        };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(problemDetails, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        // Assert - Verify key fields are present in JSON
        json.ShouldContain("\"type\":");
        json.ShouldContain("\"title\":");
        json.ShouldContain("\"status\":429");
        json.ShouldContain("\"detail\":");
        json.ShouldContain("\"retryAfter\":60");
        json.ShouldContain("\"correlationId\":");
    }
}
