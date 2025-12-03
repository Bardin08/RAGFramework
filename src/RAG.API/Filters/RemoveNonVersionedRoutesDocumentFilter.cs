using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RAG.API.Filters;

/// <summary>
/// Document filter that removes non-versioned routes from Swagger documentation.
/// Only versioned routes (e.g., /api/v1/...) are shown to avoid duplicate endpoints.
/// </summary>
public class RemoveNonVersionedRoutesDocumentFilter : IDocumentFilter
{
    /// <summary>
    /// Removes paths that don't contain version information.
    /// </summary>
    /// <param name="swaggerDoc">The OpenAPI document to modify.</param>
    /// <param name="context">The document filter context.</param>
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var pathsToRemove = swaggerDoc.Paths
            .Where(p => !p.Key.Contains("/v1/") && !p.Key.Contains("/v2/") && p.Key.StartsWith("/api/"))
            .Select(p => p.Key)
            .ToList();

        foreach (var path in pathsToRemove)
        {
            swaggerDoc.Paths.Remove(path);
        }
    }
}
