using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.Core.Authorization;
using RAG.Infrastructure.Authorization;
using Shouldly;

namespace RAG.Tests.Unit.Authorization;

public class TenantAuthorizationHandlerTests
{
    private readonly Mock<ILogger<TenantAuthorizationHandler>> _loggerMock;
    private readonly TenantAuthorizationHandler _handler;

    public TenantAuthorizationHandlerTests()
    {
        _loggerMock = new Mock<ILogger<TenantAuthorizationHandler>>();
        _handler = new TenantAuthorizationHandler(_loggerMock.Object);
    }

    [Fact]
    public async Task HandleRequirementAsync_UserWithMatchingTenant_Succeeds()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var user = CreateUserWithTenant(tenantId.ToString(), roles: new[] { ApplicationRoles.User });
        var resource = new DocumentResource { DocumentId = Guid.NewGuid(), TenantId = tenantId };
        var requirement = new TenantAuthorizationRequirement();
        var context = CreateAuthorizationContext(user, requirement, resource);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_UserWithDifferentTenant_Fails()
    {
        // Arrange
        var userTenantId = Guid.NewGuid();
        var resourceTenantId = Guid.NewGuid();
        var user = CreateUserWithTenant(userTenantId.ToString(), roles: new[] { ApplicationRoles.User });
        var resource = new DocumentResource { DocumentId = Guid.NewGuid(), TenantId = resourceTenantId };
        var requirement = new TenantAuthorizationRequirement();
        var context = CreateAuthorizationContext(user, requirement, resource);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_AdminUser_AlwaysSucceeds()
    {
        // Arrange
        var userTenantId = Guid.NewGuid();
        var resourceTenantId = Guid.NewGuid(); // Different tenant
        var user = CreateUserWithTenant(userTenantId.ToString(), roles: new[] { ApplicationRoles.Admin });
        var resource = new DocumentResource { DocumentId = Guid.NewGuid(), TenantId = resourceTenantId };
        var requirement = new TenantAuthorizationRequirement();
        var context = CreateAuthorizationContext(user, requirement, resource);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_MissingTenantClaim_Fails()
    {
        // Arrange
        var user = CreateUserWithoutTenant(roles: new[] { ApplicationRoles.User });
        var resource = new DocumentResource { DocumentId = Guid.NewGuid(), TenantId = Guid.NewGuid() };
        var requirement = new TenantAuthorizationRequirement();
        var context = CreateAuthorizationContext(user, requirement, resource);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_InvalidTenantIdFormat_Fails()
    {
        // Arrange
        var user = CreateUserWithTenant("invalid-guid", roles: new[] { ApplicationRoles.User });
        var resource = new DocumentResource { DocumentId = Guid.NewGuid(), TenantId = Guid.NewGuid() };
        var requirement = new TenantAuthorizationRequirement();
        var context = CreateAuthorizationContext(user, requirement, resource);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_AdminWithoutTenantClaim_Succeeds()
    {
        // Arrange - Admin should bypass tenant check even without tenant_id claim
        var user = CreateUserWithoutTenant(roles: new[] { ApplicationRoles.Admin });
        var resource = new DocumentResource { DocumentId = Guid.NewGuid(), TenantId = Guid.NewGuid() };
        var requirement = new TenantAuthorizationRequirement();
        var context = CreateAuthorizationContext(user, requirement, resource);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeTrue();
    }

    private static ClaimsPrincipal CreateUserWithTenant(string tenantId, string[]? roles = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user-id"),
            new("tenant_id", tenantId)
        };

        if (roles != null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateUserWithoutTenant(string[]? roles = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user-id")
        };

        if (roles != null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    private static AuthorizationHandlerContext CreateAuthorizationContext(
        ClaimsPrincipal user,
        IAuthorizationRequirement requirement,
        object? resource = null)
    {
        return new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            resource);
    }
}
