using Microsoft.AspNetCore.Http;
using RAG.Application.Interfaces;

namespace RAG.Infrastructure.Services;

/// <summary>
/// Implementation of tenant context service that extracts tenant information from JWT claims.
/// </summary>
public class TenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    /// <inheritdoc />
    public Guid GetCurrentTenantId()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null)
            throw new InvalidOperationException("HTTP context is not available");

        var tenantIdClaim = httpContext.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantIdClaim))
            throw new InvalidOperationException("Tenant ID claim is missing from JWT token");

        return Guid.TryParse(tenantIdClaim, out var tenantId)
            ? tenantId
            : throw new InvalidOperationException($"Invalid tenant ID format: {tenantIdClaim}");
    }
}