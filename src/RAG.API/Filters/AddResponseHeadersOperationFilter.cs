using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RAG.API.Filters;

/// <summary>
/// Operation filter that adds common response headers to Swagger documentation.
/// </summary>
public class AddResponseHeadersOperationFilter : IOperationFilter
{
    /// <summary>
    /// Applies common response headers to the OpenAPI operation.
    /// </summary>
    /// <param name="operation">The OpenAPI operation to modify.</param>
    /// <param name="context">The operation filter context.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Add correlation ID header to all responses
        foreach (var response in operation.Responses.Values)
        {
            response.Headers.TryAdd("X-Correlation-ID", new OpenApiHeader
            {
                Description = "Unique identifier for request tracing",
                Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
            });

            response.Headers.TryAdd("X-Request-ID", new OpenApiHeader
            {
                Description = "Request identifier for debugging",
                Schema = new OpenApiSchema { Type = "string" }
            });
        }

        // Add rate limit headers to successful responses
        if (operation.Responses.TryGetValue("200", out var successResponse))
        {
            successResponse.Headers.TryAdd("X-RateLimit-Limit", new OpenApiHeader
            {
                Description = "Maximum requests allowed per time window",
                Schema = new OpenApiSchema { Type = "integer" }
            });

            successResponse.Headers.TryAdd("X-RateLimit-Remaining", new OpenApiHeader
            {
                Description = "Remaining requests in current time window",
                Schema = new OpenApiSchema { Type = "integer" }
            });

            successResponse.Headers.TryAdd("X-RateLimit-Reset", new OpenApiHeader
            {
                Description = "Unix timestamp when the rate limit resets",
                Schema = new OpenApiSchema { Type = "integer" }
            });
        }
    }
}
