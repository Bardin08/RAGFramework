using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace RAG.API.Filters;

/// <summary>
/// Operation filter that adds authorization information to Swagger documentation.
/// Displays which authorization policies apply to each endpoint.
/// </summary>
public class AddAuthorizationHeaderOperationFilter : IOperationFilter
{
    /// <summary>
    /// Applies authorization information to the OpenAPI operation.
    /// </summary>
    /// <param name="operation">The OpenAPI operation to modify.</param>
    /// <param name="context">The operation filter context.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Get [Authorize] attributes from method and controller
        var authorizeAttributes = context.MethodInfo
            .GetCustomAttributes<AuthorizeAttribute>(true)
            .Union(context.MethodInfo.DeclaringType?
                .GetCustomAttributes<AuthorizeAttribute>(true) ?? Array.Empty<AuthorizeAttribute>())
            .ToList();

        // Get [AllowAnonymous] attribute
        var allowAnonymous = context.MethodInfo
            .GetCustomAttributes<AllowAnonymousAttribute>(true)
            .Any() ||
            (context.MethodInfo.DeclaringType?
                .GetCustomAttributes<AllowAnonymousAttribute>(true)
                .Any() ?? false);

        if (allowAnonymous)
        {
            operation.Description = PrependToDescription(operation.Description,
                "**Authentication:** Not required (public endpoint)");
            return;
        }

        if (!authorizeAttributes.Any())
        {
            return;
        }

        // Build authorization info
        var policies = authorizeAttributes
            .Where(a => !string.IsNullOrEmpty(a.Policy))
            .Select(a => a.Policy)
            .Distinct()
            .ToList();

        var roles = authorizeAttributes
            .Where(a => !string.IsNullOrEmpty(a.Roles))
            .SelectMany(a => a.Roles!.Split(',').Select(r => r.Trim()))
            .Distinct()
            .ToList();

        var authInfo = new List<string>();

        if (policies.Any())
        {
            authInfo.Add($"**Policies:** {string.Join(", ", policies)}");
        }

        if (roles.Any())
        {
            authInfo.Add($"**Required Roles:** {string.Join(", ", roles)}");
        }

        if (authInfo.Any())
        {
            var authDescription = string.Join(" | ", authInfo);
            operation.Description = PrependToDescription(operation.Description, authDescription);
        }
    }

    private static string PrependToDescription(string? existingDescription, string prefix)
    {
        if (string.IsNullOrEmpty(existingDescription))
        {
            return prefix;
        }
        return $"{prefix}\n\n{existingDescription}";
    }
}
