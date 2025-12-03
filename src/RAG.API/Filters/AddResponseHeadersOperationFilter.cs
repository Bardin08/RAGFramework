using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RAG.API.Filters;

/// <summary>
/// Operation filter that adds common response headers to Swagger documentation.
/// Includes rate limiting headers and 429 response documentation.
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

        // Add rate limit headers to all responses (successful and error)
        foreach (var response in operation.Responses.Values)
        {
            response.Headers.TryAdd("X-RateLimit-Limit", new OpenApiHeader
            {
                Description = "Maximum requests allowed per time window. Varies by user tier: Anonymous (100/min), Authenticated (200/min), Admin (500/min).",
                Schema = new OpenApiSchema { Type = "integer" }
            });

            response.Headers.TryAdd("X-RateLimit-Remaining", new OpenApiHeader
            {
                Description = "Remaining requests in current time window",
                Schema = new OpenApiSchema { Type = "integer" }
            });

            response.Headers.TryAdd("X-RateLimit-Reset", new OpenApiHeader
            {
                Description = "Unix timestamp when the rate limit resets",
                Schema = new OpenApiSchema { Type = "integer" }
            });
        }

        // Add 429 Too Many Requests response to all operations
        if (!operation.Responses.ContainsKey("429"))
        {
            operation.Responses.Add("429", new OpenApiResponse
            {
                Description = "Rate limit exceeded. The response follows RFC 7807 Problem Details format.",
                Headers = new Dictionary<string, OpenApiHeader>
                {
                    ["X-RateLimit-Limit"] = new OpenApiHeader
                    {
                        Description = "Maximum requests allowed per time window",
                        Schema = new OpenApiSchema { Type = "integer" }
                    },
                    ["X-RateLimit-Remaining"] = new OpenApiHeader
                    {
                        Description = "Remaining requests (will be 0 when rate limited)",
                        Schema = new OpenApiSchema { Type = "integer" }
                    },
                    ["X-RateLimit-Reset"] = new OpenApiHeader
                    {
                        Description = "Unix timestamp when the rate limit resets",
                        Schema = new OpenApiSchema { Type = "integer" }
                    },
                    ["Retry-After"] = new OpenApiHeader
                    {
                        Description = "Seconds until the rate limit resets",
                        Schema = new OpenApiSchema { Type = "integer" }
                    }
                },
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["type"] = new OpenApiSchema
                                {
                                    Type = "string",
                                    Example = new Microsoft.OpenApi.Any.OpenApiString("https://api.rag.system/errors/rate-limit-exceeded")
                                },
                                ["title"] = new OpenApiSchema
                                {
                                    Type = "string",
                                    Example = new Microsoft.OpenApi.Any.OpenApiString("Rate limit exceeded")
                                },
                                ["status"] = new OpenApiSchema
                                {
                                    Type = "integer",
                                    Example = new Microsoft.OpenApi.Any.OpenApiInteger(429)
                                },
                                ["detail"] = new OpenApiSchema
                                {
                                    Type = "string",
                                    Example = new Microsoft.OpenApi.Any.OpenApiString("You have exceeded the rate limit. Try again later.")
                                },
                                ["retryAfter"] = new OpenApiSchema
                                {
                                    Type = "string",
                                    Description = "Time until rate limit resets",
                                    Example = new Microsoft.OpenApi.Any.OpenApiString("60")
                                }
                            }
                        },
                        Example = new Microsoft.OpenApi.Any.OpenApiObject
                        {
                            ["type"] = new Microsoft.OpenApi.Any.OpenApiString("https://api.rag.system/errors/rate-limit-exceeded"),
                            ["title"] = new Microsoft.OpenApi.Any.OpenApiString("Rate limit exceeded"),
                            ["status"] = new Microsoft.OpenApi.Any.OpenApiInteger(429),
                            ["detail"] = new Microsoft.OpenApi.Any.OpenApiString("You have exceeded the rate limit. Try again later."),
                            ["retryAfter"] = new Microsoft.OpenApi.Any.OpenApiString("60")
                        }
                    }
                }
            });
        }
    }
}
