using System.Security.Claims;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAG.Core.Configuration;

namespace RAG.Infrastructure.RateLimiting;

/// <summary>
/// Custom client resolve contributor that extracts client ID based on user roles.
/// This enables role-based rate limiting where admins get higher limits than regular users.
/// </summary>
public class RoleBasedClientResolveContributor : IClientResolveContributor
{
    private readonly ILogger<RoleBasedClientResolveContributor> _logger;
    private readonly RateLimitSettings _settings;

    public RoleBasedClientResolveContributor(
        ILogger<RoleBasedClientResolveContributor> logger,
        IOptions<RateLimitSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    /// <summary>
    /// Resolves the client ID based on user authentication and roles.
    /// Returns a role-prefixed ID that matches ClientRateLimitPolicies rules.
    /// </summary>
    public Task<string> ResolveClientAsync(HttpContext httpContext)
    {
        var user = httpContext.User;

        // Check if user is authenticated
        if (user?.Identity?.IsAuthenticated != true)
        {
            _logger.LogDebug("Unauthenticated request - using IP-based rate limiting");
            return Task.FromResult(string.Empty); // Fall back to IP-based limiting
        }

        // Extract user ID for logging
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? user.FindFirst("sub")?.Value
                    ?? "unknown";

        // Check for admin role first (highest priority)
        if (HasRole(user, "admin"))
        {
            _logger.LogDebug("User {UserId} identified as admin - using admin rate limit tier", userId);
            return Task.FromResult("role:admin");
        }

        // Check for user role (standard authenticated user)
        if (HasRole(user, "user") || user.Identity.IsAuthenticated)
        {
            _logger.LogDebug("User {UserId} identified as authenticated user - using user rate limit tier", userId);
            return Task.FromResult("role:user");
        }

        // Default fallback - should not reach here for authenticated users
        _logger.LogDebug("User {UserId} has no recognized role - using default rate limit", userId);
        return Task.FromResult($"user:{userId}");
    }

    /// <summary>
    /// Checks if the user has a specific role.
    /// Handles multiple claim types that might contain role information.
    /// </summary>
    private static bool HasRole(ClaimsPrincipal user, string role)
    {
        // Check standard role claim
        if (user.IsInRole(role))
            return true;

        // Check realm_access roles (Keycloak format)
        var realmRoles = user.FindFirst("realm_access")?.Value;
        if (!string.IsNullOrEmpty(realmRoles) && realmRoles.Contains($"\"{role}\""))
            return true;

        // Check resource_access roles (Keycloak client roles)
        var resourceRoles = user.FindFirst("resource_access")?.Value;
        if (!string.IsNullOrEmpty(resourceRoles) && resourceRoles.Contains($"\"{role}\""))
            return true;

        // Check roles claim (array format from claims transformation)
        var rolesClaim = user.FindAll(ClaimTypes.Role)
                            .Concat(user.FindAll("roles"))
                            .Concat(user.FindAll("role"));

        return rolesClaim.Any(c => c.Value.Equals(role, StringComparison.OrdinalIgnoreCase));
    }
}
