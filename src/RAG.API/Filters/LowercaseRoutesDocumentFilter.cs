using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RAG.API.Filters;

/// <summary>
/// Document filter that converts all route paths to lowercase for consistency.
/// </summary>
public class LowercaseRoutesDocumentFilter : IDocumentFilter
{
    /// <summary>
    /// Converts all paths in the OpenAPI document to lowercase.
    /// </summary>
    /// <param name="swaggerDoc">The OpenAPI document to modify.</param>
    /// <param name="context">The document filter context.</param>
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var paths = swaggerDoc.Paths.ToList();
        swaggerDoc.Paths.Clear();

        foreach (var path in paths)
        {
            var lowercasePath = path.Key.ToLowerInvariant();
            swaggerDoc.Paths[lowercasePath] = path.Value;
        }
    }
}
