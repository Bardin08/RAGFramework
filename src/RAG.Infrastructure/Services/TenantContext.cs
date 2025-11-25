using Microsoft.AspNetCore.Http;
using RAG.Application.Interfaces;

namespace RAG.Infrastructure.Services;

/// <summary>
/// Provides access to the current tenant context extracted from HTTP context.
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
