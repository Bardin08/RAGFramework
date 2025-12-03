using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace RAG.API.Controllers;

/// <summary>
/// Base controller providing common functionality for all API controllers.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Gets the current user's ID from claims.
    /// </summary>
    /// <returns>The user ID if available, otherwise null.</returns>
    protected string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// Gets the current tenant ID from claims.
    /// </summary>
    /// <returns>The tenant ID as a GUID.</returns>
    protected Guid GetTenantId()
    {
        var tenantClaim = User.FindFirstValue("tenant_id");
        return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
    }

    /// <summary>
    /// Gets the current user's name from claims.
    /// </summary>
    /// <returns>The user name if available, otherwise null.</returns>
    protected string? GetUserName()
    {
        return User.FindFirstValue(ClaimTypes.Name);
    }

    /// <summary>
    /// Checks if the current user has a specific role.
    /// </summary>
    /// <param name="role">The role to check.</param>
    /// <returns>True if the user has the role, otherwise false.</returns>
    protected bool HasRole(string role)
    {
        return User.IsInRole(role);
    }

    /// <summary>
    /// Gets the correlation ID from request headers or generates a new one.
    /// </summary>
    /// <returns>The correlation ID for request tracing.</returns>
    protected string GetCorrelationId()
    {
        if (Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId.ToString();
        }

        return Guid.NewGuid().ToString("N")[..12];
    }
}
