using Microsoft.AspNetCore.Authorization;

namespace RAG.Infrastructure.Authorization;

/// <summary>
/// Authorization requirement for tenant-based access control.
/// Ensures users can only access resources belonging to their tenant.
/// </summary>
public class TenantAuthorizationRequirement : IAuthorizationRequirement
{
}
