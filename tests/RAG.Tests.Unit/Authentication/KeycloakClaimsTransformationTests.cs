using System.Security.Claims;
using RAG.Infrastructure.Authentication;
using Shouldly;
using Xunit;

namespace RAG.Tests.Unit.Authentication;

/// <summary>
/// Unit tests for KeycloakClaimsTransformation.
/// </summary>
public class KeycloakClaimsTransformationTests
{
    private readonly KeycloakClaimsTransformation _transformation;

    public KeycloakClaimsTransformationTests()
    {
        _transformation = new KeycloakClaimsTransformation();
    }

    [Fact]
    public async Task TransformAsync_WithUnauthenticatedIdentity_ReturnsOriginalPrincipal()
    {
        // Arrange
        var identity = new ClaimsIdentity(); // Not authenticated (no authentication type)
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldBe(principal);
        result.Claims.ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_WithAuthenticatedIdentityNoRealmAccess_ReturnsOriginalClaims()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("preferred_username", "testuser")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldNotBeNull();
        result.Identity!.IsAuthenticated.ShouldBeTrue();
        result.FindFirst("sub")?.Value.ShouldBe("user-123");
        result.FindFirst("preferred_username")?.Value.ShouldBe("testuser");
    }

    [Fact]
    public async Task TransformAsync_WithRealmAccessRoles_AddsStandardRoleClaims()
    {
        // Arrange
        var realmAccess = """{"roles":["admin","user","developer"]}""";
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("realm_access", realmAccess)
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldNotBeNull();
        result.IsInRole("admin").ShouldBeTrue();
        result.IsInRole("user").ShouldBeTrue();
        result.IsInRole("developer").ShouldBeTrue();
    }

    [Fact]
    public async Task TransformAsync_WithResourceAccessRoles_AddsClientPrefixedRoleClaims()
    {
        // Arrange
        var resourceAccess = """{"rag-api":{"roles":["read","write","admin"]}}""";
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("resource_access", resourceAccess)
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldNotBeNull();
        result.IsInRole("rag-api:read").ShouldBeTrue();
        result.IsInRole("rag-api:write").ShouldBeTrue();
        result.IsInRole("rag-api:admin").ShouldBeTrue();
    }

    [Fact]
    public async Task TransformAsync_WithBothRealmAndResourceAccess_AddsBothRoleTypes()
    {
        // Arrange
        var realmAccess = """{"roles":["realm-admin"]}""";
        var resourceAccess = """{"rag-api":{"roles":["api-user"]}}""";
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("realm_access", realmAccess),
            new Claim("resource_access", resourceAccess)
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldNotBeNull();
        result.IsInRole("realm-admin").ShouldBeTrue();
        result.IsInRole("rag-api:api-user").ShouldBeTrue();
    }

    [Fact]
    public async Task TransformAsync_WithInvalidRealmAccessJson_DoesNotThrow()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("realm_access", "invalid-json")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldNotBeNull();
        result.Identity!.IsAuthenticated.ShouldBeTrue();
        // No role claims should be added due to invalid JSON
        result.Claims.Where(c => c.Type == ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_WithInvalidResourceAccessJson_DoesNotThrow()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("resource_access", "not-valid-json")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldNotBeNull();
        result.Identity!.IsAuthenticated.ShouldBeTrue();
    }

    [Fact]
    public async Task TransformAsync_WithEmptyRolesArray_DoesNotAddRoleClaims()
    {
        // Arrange
        var realmAccess = """{"roles":[]}""";
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("realm_access", realmAccess)
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldNotBeNull();
        result.Claims.Where(c => c.Type == ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_WithNullRolesProperty_DoesNotAddRoleClaims()
    {
        // Arrange
        var realmAccess = """{"roles":null}""";
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("realm_access", realmAccess)
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldNotBeNull();
        result.Claims.Where(c => c.Type == ClaimTypes.Role).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_DoesNotDuplicateExistingRoleClaims()
    {
        // Arrange
        var realmAccess = """{"roles":["admin"]}""";
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("realm_access", realmAccess),
            new Claim(ClaimTypes.Role, "admin") // Pre-existing role claim
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldNotBeNull();
        var adminRoles = result.Claims.Where(c => c.Type == ClaimTypes.Role && c.Value == "admin");
        adminRoles.Count().ShouldBe(1); // Should not duplicate
    }

    [Fact]
    public async Task TransformAsync_WithResourceAccessForDifferentClient_DoesNotAddRoles()
    {
        // Arrange
        var resourceAccess = """{"other-api":{"roles":["read","write"]}}""";
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("resource_access", resourceAccess)
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldNotBeNull();
        // No rag-api roles should be added
        result.Claims.Where(c => c.Type == ClaimTypes.Role && c.Value.StartsWith("rag-api:")).ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAsync_WithExistingTenantIdClaim_PreservesClaim()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("tenant_id", "tenant-abc-123")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldNotBeNull();
        result.FindFirst("tenant_id")?.Value.ShouldBe("tenant-abc-123");
    }

    [Fact]
    public async Task TransformAsync_WithMultipleResourceAccessClients_OnlyProcessesRagApi()
    {
        // Arrange
        var resourceAccess = """{"rag-api":{"roles":["api-user"]},"other-service":{"roles":["other-role"]}}""";
        var claims = new[]
        {
            new Claim("sub", "user-123"),
            new Claim("resource_access", resourceAccess)
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.ShouldNotBeNull();
        result.IsInRole("rag-api:api-user").ShouldBeTrue();
        result.IsInRole("other-service:other-role").ShouldBeFalse();
    }
}
