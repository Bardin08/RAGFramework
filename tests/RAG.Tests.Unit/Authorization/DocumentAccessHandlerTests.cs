using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.Application.Interfaces;
using RAG.Core.Domain.Enums;
using RAG.Infrastructure.Authorization;
using Shouldly;

namespace RAG.Tests.Unit.Authorization;

public class DocumentAccessHandlerTests
{
    private readonly Mock<IDocumentPermissionService> _permissionServiceMock;
    private readonly Mock<ILogger<DocumentAccessHandler>> _loggerMock;
    private readonly DocumentAccessHandler _handler;

    private readonly Guid _testDocumentId = Guid.NewGuid();
    private readonly Guid _testUserId = Guid.NewGuid();

    public DocumentAccessHandlerTests()
    {
        _permissionServiceMock = new Mock<IDocumentPermissionService>();
        _loggerMock = new Mock<ILogger<DocumentAccessHandler>>();
        _handler = new DocumentAccessHandler(
            _permissionServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task HandleRequirementAsync_WithValidAccess_Succeeds()
    {
        // Arrange
        var user = CreateUser(_testUserId);
        var requirement = DocumentAccessRequirement.Read;
        var context = CreateAuthorizationContext(user, requirement, _testDocumentId);

        _permissionServiceMock
            .Setup(x => x.CanAccessAsync(
                _testDocumentId,
                It.IsAny<ClaimsPrincipal>(),
                PermissionType.Read,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithoutAccess_DoesNotSucceed()
    {
        // Arrange
        var user = CreateUser(_testUserId);
        var requirement = DocumentAccessRequirement.Write;
        var context = CreateAuthorizationContext(user, requirement, _testDocumentId);

        _permissionServiceMock
            .Setup(x => x.CanAccessAsync(
                _testDocumentId,
                It.IsAny<ClaimsPrincipal>(),
                PermissionType.Write,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_WithEmptyDocumentId_DoesNotSucceed()
    {
        // Arrange
        var user = CreateUser(_testUserId);
        var requirement = DocumentAccessRequirement.Read;
        var context = CreateAuthorizationContext(user, requirement, Guid.Empty);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
        _permissionServiceMock.Verify(
            x => x.CanAccessAsync(It.IsAny<Guid>(), It.IsAny<ClaimsPrincipal>(),
                It.IsAny<PermissionType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(PermissionType.Read)]
    [InlineData(PermissionType.Write)]
    [InlineData(PermissionType.Admin)]
    public async Task HandleRequirementAsync_PassesCorrectPermissionType(PermissionType expectedPermission)
    {
        // Arrange
        var user = CreateUser(_testUserId);
        var requirement = new DocumentAccessRequirement(expectedPermission);
        var context = CreateAuthorizationContext(user, requirement, _testDocumentId);

        PermissionType capturedPermission = default;
        _permissionServiceMock
            .Setup(x => x.CanAccessAsync(
                _testDocumentId,
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<PermissionType>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, ClaimsPrincipal, PermissionType, CancellationToken>((_, _, p, _) => capturedPermission = p)
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        capturedPermission.ShouldBe(expectedPermission);
    }

    [Fact]
    public void StaticRequirements_ReturnCorrectPermissionTypes()
    {
        // Assert
        DocumentAccessRequirement.Read.RequiredPermission.ShouldBe(PermissionType.Read);
        DocumentAccessRequirement.Write.RequiredPermission.ShouldBe(PermissionType.Write);
        DocumentAccessRequirement.Admin.RequiredPermission.ShouldBe(PermissionType.Admin);
    }

    #region Helper Methods

    private static ClaimsPrincipal CreateUser(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "test");
        return new ClaimsPrincipal(identity);
    }

    private static AuthorizationHandlerContext CreateAuthorizationContext(
        ClaimsPrincipal user,
        IAuthorizationRequirement requirement,
        Guid resource)
    {
        return new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            resource);
    }

    #endregion
}
