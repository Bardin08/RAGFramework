using System.Text.RegularExpressions;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RAG.API.Filters;

/// <summary>
/// Document filter that converts all route paths to lowercase for consistency,
/// while preserving the case of route parameters for Swagger UI compatibility.
/// </summary>
public partial class LowercaseRoutesDocumentFilter : IDocumentFilter
{
    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex RouteParameterRegex();

    /// <summary>
    /// Converts all paths in the OpenAPI document to lowercase,
    /// preserving route parameter names (e.g., {jobId} stays as {jobId}).
    /// </summary>
    /// <param name="swaggerDoc">The OpenAPI document to modify.</param>
    /// <param name="context">The document filter context.</param>
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var paths = swaggerDoc.Paths.ToList();
        swaggerDoc.Paths.Clear();

        foreach (var path in paths)
        {
            var lowercasePath = LowercasePathPreservingParameters(path.Key);
            swaggerDoc.Paths[lowercasePath] = path.Value;
        }
    }

    /// <summary>
    /// Converts path segments to lowercase while preserving route parameter names.
    /// </summary>
    private static string LowercasePathPreservingParameters(string path)
    {
        // Replace route parameters with placeholders, lowercase the rest, then restore parameters
        var parameters = new List<string>();
        var parameterRegex = RouteParameterRegex();

        // Extract and store all parameters
        var matches = parameterRegex.Matches(path);
        foreach (Match match in matches)
        {
            parameters.Add(match.Value);
        }

        // Replace parameters with indexed placeholders
        var tempPath = path;
        for (int i = 0; i < parameters.Count; i++)
        {
            tempPath = tempPath.Replace(parameters[i], $"__PARAM_{i}__");
        }

        // Lowercase the path
        tempPath = tempPath.ToLowerInvariant();

        // Restore parameters
        for (int i = 0; i < parameters.Count; i++)
        {
            tempPath = tempPath.Replace($"__param_{i}__", parameters[i]);
        }

        return tempPath;
    }
}
