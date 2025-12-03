using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using RAG.Core.Authorization;

namespace RAG.Infrastructure.Authorization;

/// <summary>
/// Authorization handler for tenant-based access control.
/// Verifies that the user belongs to the same tenant as the document resource.
/// Admin users bypass tenant checks.
/// </summary>
public class TenantAuthorizationHandler : AuthorizationHandler<TenantAuthorizationRequirement, DocumentResource>
{
    private const string TenantIdClaimType = "tenant_id";
    private readonly ILogger<TenantAuthorizationHandler> _logger;

    public TenantAuthorizationHandler(ILogger<TenantAuthorizationHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantAuthorizationRequirement requirement,
        DocumentResource resource)
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? context.User.FindFirst("sub")?.Value;

        // Admin bypass: admins can access any tenant's resources
        if (context.User.IsInRole(ApplicationRoles.Admin))
        {
            _logger.LogDebug(
                "Admin bypass: User {UserId} granted access to document {DocumentId} (admin role)",
                userId, resource.DocumentId);
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Extract tenant_id from user claims
        var userTenantIdClaim = context.User.FindFirst(TenantIdClaimType)?.Value;

        if (string.IsNullOrEmpty(userTenantIdClaim))
        {
            _logger.LogWarning(
                "Authorization failed: User {UserId} has no tenant_id claim",
                userId);
            return Task.CompletedTask; // Fail requirement (do not call Succeed)
        }

        if (!Guid.TryParse(userTenantIdClaim, out var userTenantId))
        {
            _logger.LogWarning(
                "Authorization failed: User {UserId} has invalid tenant_id claim: {TenantIdClaim}",
                userId, userTenantIdClaim);
            return Task.CompletedTask;
        }

        // Tenant match check
        if (userTenantId == resource.TenantId)
        {
            _logger.LogDebug(
                "Tenant match: User {UserId} granted access to document {DocumentId} (tenant {TenantId})",
                userId, resource.DocumentId, resource.TenantId);
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning(
                "Authorization failed: User {UserId} (tenant {UserTenantId}) cannot access document {DocumentId} (tenant {ResourceTenantId})",
                userId, userTenantId, resource.DocumentId, resource.TenantId);
        }

        return Task.CompletedTask;
    }
}
