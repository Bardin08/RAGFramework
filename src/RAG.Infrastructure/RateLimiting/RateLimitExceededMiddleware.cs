using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace RAG.Infrastructure.RateLimiting;

/// <summary>
/// Middleware that logs rate limit violations.
/// The actual 429 response is handled by AspNetCoreRateLimit with custom QuotaExceededResponse.
/// </summary>
public class RateLimitExceededMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitExceededMiddleware> _logger;

    public RateLimitExceededMiddleware(
        RequestDelegate next,
        ILogger<RateLimitExceededMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        // Log rate limit violations after response is complete
        if (context.Response.StatusCode == StatusCodes.Status429TooManyRequests)
        {
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var endpoint = context.Request.Path;
            var userId = context.User?.Identity?.IsAuthenticated == true
                ? context.User.FindFirst("sub")?.Value ?? "authenticated"
                : "anonymous";

            _logger.LogWarning(
                "Rate limit exceeded for client {ClientIp}, user {UserId}, endpoint {Endpoint}",
                clientIp,
                userId,
                endpoint);
        }
    }
}

/// <summary>
/// RFC 7807 Problem Details with rate limit specific fields.
/// </summary>
public class RateLimitProblemDetails
{
    /// <summary>
    /// A URI reference that identifies the problem type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// A short, human-readable summary of the problem type.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The HTTP status code.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// A human-readable explanation specific to this occurrence.
    /// </summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>
    /// A URI reference that identifies the specific occurrence.
    /// </summary>
    public string Instance { get; set; } = string.Empty;

    /// <summary>
    /// Number of seconds until the rate limit resets.
    /// </summary>
    public int RetryAfter { get; set; }

    /// <summary>
    /// Correlation ID for request tracing.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the error occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}
