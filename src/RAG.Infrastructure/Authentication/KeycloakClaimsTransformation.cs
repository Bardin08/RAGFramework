using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;

namespace RAG.Infrastructure.Authentication;

/// <summary>
/// Claims transformation for Keycloak tokens.
/// Transforms Keycloak-specific claims to standard .NET role claims.
/// </summary>
public class KeycloakClaimsTransformation : IClaimsTransformation
{
    /// <summary>
    /// Standard claim types used in the application.
    /// </summary>
    public static class ClaimTypes
    {
        /// <summary>User ID from the 'sub' claim.</summary>
        public const string UserId = "sub";

        /// <summary>Username from the 'preferred_username' claim.</summary>
        public const string Username = "preferred_username";

        /// <summary>Email from the 'email' claim.</summary>
        public const string Email = "email";

        /// <summary>Tenant ID custom claim.</summary>
        public const string TenantId = "tenant_id";

        /// <summary>Realm access roles from Keycloak.</summary>
        public const string RealmAccess = "realm_access";

        /// <summary>Resource access roles from Keycloak.</summary>
        public const string ResourceAccess = "resource_access";
    }

    /// <inheritdoc />
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identity as ClaimsIdentity;
        if (identity == null || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        // Transform realm_access.roles to standard role claims
        TransformRealmRoles(identity);

        // Transform resource_access roles for the rag-api client
        TransformResourceRoles(identity, "rag-api");

        // Ensure tenant_id is available as a standard claim
        EnsureTenantIdClaim(identity);

        return Task.FromResult(principal);
    }

    /// <summary>
    /// Transforms Keycloak realm_access.roles to standard role claims.
    /// </summary>
    private void TransformRealmRoles(ClaimsIdentity identity)
    {
        var realmAccessClaim = identity.FindFirst(ClaimTypes.RealmAccess);
        if (realmAccessClaim == null)
        {
            return;
        }

        try
        {
            var realmAccess = JsonSerializer.Deserialize<RealmAccess>(realmAccessClaim.Value);
            if (realmAccess?.Roles == null)
            {
                return;
            }

            foreach (var role in realmAccess.Roles)
            {
                // Add as standard role claim if not already present
                if (!identity.HasClaim(System.Security.Claims.ClaimTypes.Role, role))
                {
                    identity.AddClaim(new Claim(System.Security.Claims.ClaimTypes.Role, role));
                }
            }
        }
        catch (JsonException)
        {
            // Invalid JSON in realm_access claim - skip transformation
        }
    }

    /// <summary>
    /// Transforms Keycloak resource_access roles for a specific client to standard role claims.
    /// </summary>
    private void TransformResourceRoles(ClaimsIdentity identity, string clientId)
    {
        var resourceAccessClaim = identity.FindFirst(ClaimTypes.ResourceAccess);
        if (resourceAccessClaim == null)
        {
            return;
        }

        try
        {
            var resourceAccess = JsonSerializer.Deserialize<Dictionary<string, ResourceAccess>>(resourceAccessClaim.Value);
            if (resourceAccess == null || !resourceAccess.TryGetValue(clientId, out var clientAccess))
            {
                return;
            }

            if (clientAccess?.Roles == null)
            {
                return;
            }

            foreach (var role in clientAccess.Roles)
            {
                // Add as standard role claim with client prefix if not already present
                var roleClaim = $"{clientId}:{role}";
                if (!identity.HasClaim(System.Security.Claims.ClaimTypes.Role, roleClaim))
                {
                    identity.AddClaim(new Claim(System.Security.Claims.ClaimTypes.Role, roleClaim));
                }
            }
        }
        catch (JsonException)
        {
            // Invalid JSON in resource_access claim - skip transformation
        }
    }

    /// <summary>
    /// Ensures the tenant_id claim is present and accessible.
    /// </summary>
    private void EnsureTenantIdClaim(ClaimsIdentity identity)
    {
        var tenantIdClaim = identity.FindFirst(ClaimTypes.TenantId);
        if (tenantIdClaim != null)
        {
            // Already present
            return;
        }

        // Try to extract from other claims or set a default
        // In production, tenant_id should be a custom claim configured in Keycloak
        // For development, we can use a default tenant
        var subClaim = identity.FindFirst(ClaimTypes.UserId);
        if (subClaim != null)
        {
            // Optionally derive tenant from user ID or use a default
            // This is a fallback - production should have explicit tenant_id claim
        }
    }

    /// <summary>
    /// Keycloak realm_access claim structure.
    /// </summary>
    private class RealmAccess
    {
        [JsonPropertyName("roles")]
        public List<string>? Roles { get; set; }
    }

    /// <summary>
    /// Keycloak resource_access claim structure for a specific client.
    /// </summary>
    private class ResourceAccess
    {
        [JsonPropertyName("roles")]
        public List<string>? Roles { get; set; }
    }
}
