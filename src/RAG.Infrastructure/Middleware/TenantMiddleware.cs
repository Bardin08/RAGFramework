using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;

namespace RAG.Infrastructure.Middleware;

/// <summary>
/// Middleware that validates and logs tenant context for authenticated requests.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // Only process authenticated requests
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            if (tenantContext.TryGetTenantId(out var tenantId))
            {
                _logger.LogDebug("Request authenticated for tenant: {TenantId}", tenantId);
                
                // Store tenant ID in HttpContext items for easy access
                context.Items["TenantId"] = tenantId;
                context.Items["IsGlobalAdmin"] = tenantContext.IsGlobalAdmin;
            }
            else if (!tenantContext.IsGlobalAdmin)
            {
                // Authenticated but no tenant_id claim (and not a global admin)
                _logger.LogWarning(
                    "Authenticated request missing tenant_id claim. User: {User}, Path: {Path}",
                    context.User.Identity.Name ?? "Unknown",
                    context.Request.Path);
                
                // For now, we'll allow the request to continue but log the warning
                // In production, you might want to return 401 Unauthorized here
                // context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                // await context.Response.WriteAsJsonAsync(new { error = "Missing tenant_id claim" });
                // return;
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering tenant middleware.
/// </summary>
public static class TenantMiddlewareExtensions
{
    /// <summary>
    /// Adds the tenant middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantMiddleware>();
    }
}
