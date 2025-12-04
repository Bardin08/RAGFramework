using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RAG.Application.Interfaces;

namespace RAG.Infrastructure.Services;

/// <summary>
/// Provides access to the current tenant and user context extracted from HTTP context.
/// </summary>
public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string TenantIdClaimType = "tenant_id";
    private const string GlobalAdminClaimType = "admin:global";

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc />
    public Guid GetTenantId()
    {
        if (!TryGetTenantId(out var tenantId))
        {
            throw new InvalidOperationException(
                "Tenant context is not available. Ensure the request contains a valid tenant_id claim.");
        }

        return tenantId;
    }

    /// <inheritdoc />
    public bool TryGetTenantId(out Guid tenantId)
    {
        tenantId = Guid.Empty;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var tenantIdClaim = httpContext.User.FindFirst(TenantIdClaimType);
        if (tenantIdClaim == null)
        {
            return false;
        }

        return Guid.TryParse(tenantIdClaim.Value, out tenantId) && tenantId != Guid.Empty;
    }

    /// <inheritdoc />
    public Guid GetUserId()
    {
        if (!TryGetUserId(out var userId))
        {
            throw new InvalidOperationException(
                "User context is not available. Ensure the request contains a valid sub claim.");
        }

        return userId;
    }

    /// <inheritdoc />
    public bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        // Look for user ID in order of preference:
        // 1. Standard NameIdentifier claim (mapped from sub)
        // 2. Raw 'sub' claim from JWT
        // 3. Keycloak session ID 'sid' as fallback (not ideal but works for testing)
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)
                       ?? httpContext.User.FindFirst("sub")
                       ?? httpContext.User.FindFirst("sid");

        if (userIdClaim == null)
        {
            return false;
        }

        return Guid.TryParse(userIdClaim.Value, out userId) && userId != Guid.Empty;
    }

    /// <inheritdoc />
    public bool IsGlobalAdmin
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            return httpContext.User.HasClaim(c => c.Type == GlobalAdminClaimType);
        }
    }
}
