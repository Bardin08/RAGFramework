using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RAG.Application.Interfaces;
using RAG.Core.Authorization;
using RAG.Infrastructure.Repositories;
using RAG.Infrastructure.Services;

namespace RAG.Infrastructure.Authorization;

/// <summary>
/// Extension methods for configuring authorization services.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Adds RBAC authorization policies and handlers.
    /// </summary>
    public static IServiceCollection AddRbacAuthorization(this IServiceCollection services)
    {
        // Register authorization handlers
        services.AddSingleton<IAuthorizationHandler, TenantAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, DocumentAccessHandler>();

        // Register document authorization service (legacy)
        services.AddScoped<IDocumentAuthorizationService, DocumentAuthorizationService>();

        // Register ACL services
        services.AddScoped<IDocumentAccessRepository, DocumentAccessRepository>();
        services.AddScoped<IDocumentPermissionService, DocumentPermissionService>();
        services.AddScoped<IUserLookupService, KeycloakUserLookupService>();

        return services;
    }

    /// <summary>
    /// Configures authorization policies for the application.
    /// </summary>
    public static AuthorizationOptions AddRbacPolicies(this AuthorizationOptions options)
    {
        // AdminOnly: Requires admin role
        options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
            policy.RequireRole(ApplicationRoles.Admin));

        // UserOrAdmin: Requires either user or admin role
        options.AddPolicy(AuthorizationPolicies.UserOrAdmin, policy =>
            policy.RequireRole(ApplicationRoles.User, ApplicationRoles.Admin));

        // SameTenant: Custom requirement for resource-based tenant verification
        options.AddPolicy(AuthorizationPolicies.SameTenant, policy =>
            policy.Requirements.Add(new TenantAuthorizationRequirement()));

        return options;
    }
}
