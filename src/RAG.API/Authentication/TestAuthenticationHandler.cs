using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace RAG.API.Authentication;

/// <summary>
/// Development-only authentication handler that accepts test tokens with role support.
/// Supports special tokens for different roles:
/// - "admin-token" → admin + user roles
/// - "user-token" → user role only
/// - "dev-test-token-12345" → admin + user roles (backward compatible)
/// - Any JWT token → attempts to validate against Keycloak (falls through to JWT handler)
/// DO NOT USE IN PRODUCTION.
/// </summary>
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    // Constant test token for development (backward compatible)
    public const string TestToken = "dev-test-token-12345";

    // Test tenant IDs for development
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AlternateTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Get Authorization header
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = authHeader.ToString();

        // Remove "Bearer " prefix if present
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token.Substring(7);
        }

        // Check if it's a known test token
        var (isTestToken, roles, tenantId) = ParseTestToken(token);

        if (!isTestToken)
        {
            // Not a test token - let JWT Bearer handler try to validate it
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Create claims for the test user
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(ClaimTypes.Email, "test@example.com")
        };

        // Add role claims
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogInformation(
            "Test authentication successful for tenant {TenantId} with roles: {Roles}",
            tenantId,
            string.Join(", ", roles));

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static (bool isTestToken, string[] roles, Guid tenantId) ParseTestToken(string token)
    {
        return token.ToLowerInvariant() switch
        {
            "admin-token" => (true, new[] { "admin", "user" }, DefaultTenantId),
            "user-token" => (true, new[] { "user" }, DefaultTenantId),
            "no-role-token" => (true, Array.Empty<string>(), DefaultTenantId),
            "cross-tenant-token" => (true, new[] { "user" }, AlternateTenantId),
            "dev-test-token-12345" => (true, new[] { "admin", "user" }, DefaultTenantId), // Backward compatible
            _ => (false, Array.Empty<string>(), DefaultTenantId) // Not a test token
        };
    }
}
