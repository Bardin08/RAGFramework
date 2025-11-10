using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace RAG.API.Authentication;

/// <summary>
/// Development-only authentication handler that accepts a constant test token.
/// DO NOT USE IN PRODUCTION.
/// </summary>
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    // Constant test token for development
    public const string TestToken = "dev-test-token-12345";

    // Test tenant ID for development
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

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
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header"));
        }

        var token = authHeader.ToString();

        // Remove "Bearer " prefix if present
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token.Substring(7);
        }

        // Check if token matches our test token
        if (token != TestToken)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid test token"));
        }

        // Create claims for the test user
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim("tenant_id", TestTenantId.ToString()),
            new Claim(ClaimTypes.Email, "test@example.com")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogInformation("Test authentication successful for tenant {TenantId}", TestTenantId);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
