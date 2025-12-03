using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RAG.Core.Configuration;
using RAG.Infrastructure.RateLimiting;
using Shouldly;

namespace RAG.Tests.Unit.RateLimiting;

/// <summary>
/// Unit tests for RoleBasedClientResolveContributor.
/// Tests policy selection based on user roles (AC: 6).
/// </summary>
public class RoleBasedClientResolveContributorTests
{
    private readonly Mock<ILogger<RoleBasedClientResolveContributor>> _loggerMock;
    private readonly IOptions<RateLimitSettings> _settings;
    private readonly RoleBasedClientResolveContributor _contributor;

    public RoleBasedClientResolveContributorTests()
    {
        _loggerMock = new Mock<ILogger<RoleBasedClientResolveContributor>>();
        _settings = Options.Create(new RateLimitSettings
        {
            Tiers = new RateLimitTiers
            {
                Anonymous = 100,
                Authenticated = 200,
                Admin = 500
            }
        });
        _contributor = new RoleBasedClientResolveContributor(_loggerMock.Object, _settings);
    }

    [Fact]
    public async Task ResolveClientAsync_UnauthenticatedUser_ReturnsEmptyString()
    {
        // Arrange
        var httpContext = CreateHttpContext(isAuthenticated: false);

        // Act
        var result = await _contributor.ResolveClientAsync(httpContext);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveClientAsync_AdminUser_ReturnsAdminRoleId()
    {
        // Arrange
        var httpContext = CreateHttpContext(isAuthenticated: true, roles: new[] { "admin" });

        // Act
        var result = await _contributor.ResolveClientAsync(httpContext);

        // Assert
        result.ShouldBe("role:admin");
    }

    [Fact]
    public async Task ResolveClientAsync_RegularUser_ReturnsUserRoleId()
    {
        // Arrange
        var httpContext = CreateHttpContext(isAuthenticated: true, roles: new[] { "user" });

        // Act
        var result = await _contributor.ResolveClientAsync(httpContext);

        // Assert
        result.ShouldBe("role:user");
    }

    [Fact]
    public async Task ResolveClientAsync_UserWithBothRoles_ReturnsAdminRoleId()
    {
        // Arrange - Admin role should have priority
        var httpContext = CreateHttpContext(isAuthenticated: true, roles: new[] { "user", "admin" });

        // Act
        var result = await _contributor.ResolveClientAsync(httpContext);

        // Assert
        result.ShouldBe("role:admin");
    }

    [Fact]
    public async Task ResolveClientAsync_AuthenticatedNoExplicitRole_ReturnsUserRoleId()
    {
        // Arrange - Authenticated users without explicit role still get user tier
        var httpContext = CreateHttpContext(isAuthenticated: true, roles: Array.Empty<string>());

        // Act
        var result = await _contributor.ResolveClientAsync(httpContext);

        // Assert
        result.ShouldBe("role:user");
    }

    [Fact]
    public async Task ResolveClientAsync_UserWithViewerRole_ReturnsUserRoleId()
    {
        // Arrange - Viewer is authenticated but not explicitly admin
        var httpContext = CreateHttpContext(isAuthenticated: true, roles: new[] { "viewer" });

        // Act
        var result = await _contributor.ResolveClientAsync(httpContext);

        // Assert
        result.ShouldBe("role:user"); // Authenticated users default to user tier
    }

    [Fact]
    public async Task ResolveClientAsync_NullUser_ReturnsEmptyString()
    {
        // Arrange
        var httpContext = new DefaultHttpContext
        {
            User = null!
        };

        // Act
        var result = await _contributor.ResolveClientAsync(httpContext);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveClientAsync_CaseInsensitiveRoleMatching()
    {
        // Arrange
        var httpContext = CreateHttpContext(isAuthenticated: true, roles: new[] { "ADMIN" });

        // Act
        var result = await _contributor.ResolveClientAsync(httpContext);

        // Assert
        result.ShouldBe("role:admin");
    }

    private static HttpContext CreateHttpContext(bool isAuthenticated, string[]? roles = null, string? userId = null)
    {
        var claims = new List<Claim>();

        if (isAuthenticated)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId ?? "test-user-123"));
            claims.Add(new Claim("sub", userId ?? "test-user-123"));

            if (roles != null)
            {
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }
        }

        var identity = new ClaimsIdentity(claims, isAuthenticated ? "TestAuth" : null);
        var principal = new ClaimsPrincipal(identity);

        return new DefaultHttpContext
        {
            User = principal
        };
    }
}
