using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using RAG.Application.Interfaces;
using RAG.Core.Authorization;
using RAG.Core.Domain;
using RAG.Core.Domain.Enums;
using RAG.Infrastructure.Services;
using Shouldly;

namespace RAG.Tests.Unit.Services;

public class DocumentPermissionServiceTests
{
    private readonly Mock<IDocumentAccessRepository> _accessRepositoryMock;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<DocumentPermissionService>> _loggerMock;
    private readonly DocumentPermissionService _service;

    private readonly Guid _testDocumentId = Guid.NewGuid();
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _testTenantId = Guid.NewGuid();
    private readonly Guid _testOwnerId = Guid.NewGuid();

    public DocumentPermissionServiceTests()
    {
        _accessRepositoryMock = new Mock<IDocumentAccessRepository>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<DocumentPermissionService>>();
        _service = new DocumentPermissionService(
            _accessRepositoryMock.Object,
            _cache,
            _loggerMock.Object);
    }

    #region Owner Access Tests

    [Fact]
    public async Task CanAccessAsync_OwnerHasFullAccess_ReturnsTrue()
    {
        // Arrange
        var user = CreateUser(_testOwnerId, _testTenantId);
        var document = CreateDocument(_testDocumentId, _testTenantId, _testOwnerId);

        _accessRepositoryMock
            .Setup(x => x.GetDocumentWithOwnerAsync(_testDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act
        var canRead = await _service.CanAccessAsync(_testDocumentId, user, PermissionType.Read);
        var canWrite = await _service.CanAccessAsync(_testDocumentId, user, PermissionType.Write);
        var canAdmin = await _service.CanAccessAsync(_testDocumentId, user, PermissionType.Admin);

        // Assert
        canRead.ShouldBeTrue();
        canWrite.ShouldBeTrue();
        canAdmin.ShouldBeTrue();
    }

    #endregion

    #region Admin Role Bypass Tests

    [Fact]
    public async Task CanAccessAsync_AdminRoleBypassesPermissionCheck_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithRole(_testUserId, _testTenantId, ApplicationRoles.Admin);

        // Don't set up repository - admin should bypass before checking

        // Act
        var result = await _service.CanAccessAsync(_testDocumentId, user, PermissionType.Admin);

        // Assert
        result.ShouldBeTrue();
        _accessRepositoryMock.Verify(
            x => x.GetDocumentWithOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Explicit Permission Tests

    [Theory]
    [InlineData(PermissionType.Read, PermissionType.Read, true)]
    [InlineData(PermissionType.Read, PermissionType.Write, false)]
    [InlineData(PermissionType.Read, PermissionType.Admin, false)]
    [InlineData(PermissionType.Write, PermissionType.Read, true)]
    [InlineData(PermissionType.Write, PermissionType.Write, true)]
    [InlineData(PermissionType.Write, PermissionType.Admin, false)]
    [InlineData(PermissionType.Admin, PermissionType.Read, true)]
    [InlineData(PermissionType.Admin, PermissionType.Write, true)]
    [InlineData(PermissionType.Admin, PermissionType.Admin, true)]
    public async Task CanAccessAsync_ExplicitPermission_RespectsInheritance(
        PermissionType granted,
        PermissionType required,
        bool expectedResult)
    {
        // Arrange
        var user = CreateUser(_testUserId, _testTenantId);
        var document = CreateDocument(_testDocumentId, _testTenantId, _testOwnerId);

        _accessRepositoryMock
            .Setup(x => x.GetDocumentWithOwnerAsync(_testDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _accessRepositoryMock
            .Setup(x => x.GetAccessAsync(_testDocumentId, _testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentAccess(
                Guid.NewGuid(),
                _testDocumentId,
                _testUserId,
                granted,
                _testOwnerId));

        // Act
        var result = await _service.CanAccessAsync(_testDocumentId, user, required);

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public async Task CanAccessAsync_NoExplicitPermission_ReturnsFalse()
    {
        // Arrange
        var user = CreateUser(_testUserId, _testTenantId);
        var document = CreateDocument(_testDocumentId, _testTenantId, _testOwnerId, isPublic: false);

        _accessRepositoryMock
            .Setup(x => x.GetDocumentWithOwnerAsync(_testDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _accessRepositoryMock
            .Setup(x => x.GetAccessAsync(_testDocumentId, _testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentAccess?)null);

        // Act
        var result = await _service.CanAccessAsync(_testDocumentId, user, PermissionType.Read);

        // Assert
        result.ShouldBeFalse();
    }

    #endregion

    #region Public Document Tests

    [Fact]
    public async Task CanAccessAsync_PublicDocumentAllowsReadForSameTenant_ReturnsTrue()
    {
        // Arrange
        var user = CreateUser(_testUserId, _testTenantId);
        var document = CreateDocument(_testDocumentId, _testTenantId, _testOwnerId, isPublic: true);

        _accessRepositoryMock
            .Setup(x => x.GetDocumentWithOwnerAsync(_testDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _accessRepositoryMock
            .Setup(x => x.GetAccessAsync(_testDocumentId, _testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentAccess?)null);

        // Act
        var result = await _service.CanAccessAsync(_testDocumentId, user, PermissionType.Read);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CanAccessAsync_PublicDocumentDeniesWriteWithoutExplicitPermission_ReturnsFalse()
    {
        // Arrange
        var user = CreateUser(_testUserId, _testTenantId);
        var document = CreateDocument(_testDocumentId, _testTenantId, _testOwnerId, isPublic: true);

        _accessRepositoryMock
            .Setup(x => x.GetDocumentWithOwnerAsync(_testDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _accessRepositoryMock
            .Setup(x => x.GetAccessAsync(_testDocumentId, _testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentAccess?)null);

        // Act
        var result = await _service.CanAccessAsync(_testDocumentId, user, PermissionType.Write);

        // Assert
        result.ShouldBeFalse();
    }

    #endregion

    #region Tenant Isolation Tests

    [Fact]
    public async Task CanAccessAsync_CrossTenantAccess_ReturnsFalse()
    {
        // Arrange
        var differentTenantId = Guid.NewGuid();
        var user = CreateUser(_testUserId, _testTenantId);
        var document = CreateDocument(_testDocumentId, differentTenantId, _testOwnerId);

        _accessRepositoryMock
            .Setup(x => x.GetDocumentWithOwnerAsync(_testDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act
        var result = await _service.CanAccessAsync(_testDocumentId, user, PermissionType.Read);

        // Assert
        result.ShouldBeFalse();
    }

    #endregion

    #region Cache Tests

    [Fact]
    public async Task CanAccessAsync_CachesPermissionResult()
    {
        // Arrange
        var user = CreateUser(_testUserId, _testTenantId);
        var document = CreateDocument(_testDocumentId, _testTenantId, _testOwnerId);

        _accessRepositoryMock
            .Setup(x => x.GetDocumentWithOwnerAsync(_testDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _accessRepositoryMock
            .Setup(x => x.GetAccessAsync(_testDocumentId, _testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentAccess(
                Guid.NewGuid(),
                _testDocumentId,
                _testUserId,
                PermissionType.Read,
                _testOwnerId));

        // Act - First call
        await _service.CanAccessAsync(_testDocumentId, user, PermissionType.Read);

        // Act - Second call (should use cache)
        await _service.CanAccessAsync(_testDocumentId, user, PermissionType.Read);

        // Assert - GetAccessAsync should only be called once
        _accessRepositoryMock.Verify(
            x => x.GetAccessAsync(_testDocumentId, _testUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void InvalidateCache_RemovesCachedPermission()
    {
        // Arrange
        var cacheKey = $"permission:{_testUserId}:{_testDocumentId}";
        _cache.Set(cacheKey, (PermissionType?)PermissionType.Read);

        // Act
        _service.InvalidateCache(_testDocumentId, _testUserId);

        // Assert
        _cache.TryGetValue(cacheKey, out _).ShouldBeFalse();
    }

    #endregion

    #region GetEffectivePermission Tests

    [Fact]
    public async Task GetEffectivePermissionAsync_OwnerReturnsAdmin()
    {
        // Arrange
        var document = CreateDocument(_testDocumentId, _testTenantId, _testUserId);

        _accessRepositoryMock
            .Setup(x => x.GetDocumentWithOwnerAsync(_testDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act
        var result = await _service.GetEffectivePermissionAsync(_testDocumentId, _testUserId);

        // Assert
        result.ShouldBe(PermissionType.Admin);
    }

    [Fact]
    public async Task GetEffectivePermissionAsync_ExplicitPermissionReturnsGranted()
    {
        // Arrange
        var document = CreateDocument(_testDocumentId, _testTenantId, _testOwnerId);

        _accessRepositoryMock
            .Setup(x => x.GetDocumentWithOwnerAsync(_testDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _accessRepositoryMock
            .Setup(x => x.GetAccessAsync(_testDocumentId, _testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentAccess(
                Guid.NewGuid(),
                _testDocumentId,
                _testUserId,
                PermissionType.Write,
                _testOwnerId));

        // Act
        var result = await _service.GetEffectivePermissionAsync(_testDocumentId, _testUserId);

        // Assert
        result.ShouldBe(PermissionType.Write);
    }

    [Fact]
    public async Task GetEffectivePermissionAsync_NoAccessReturnsNull()
    {
        // Arrange
        var document = CreateDocument(_testDocumentId, _testTenantId, _testOwnerId);

        _accessRepositoryMock
            .Setup(x => x.GetDocumentWithOwnerAsync(_testDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _accessRepositoryMock
            .Setup(x => x.GetAccessAsync(_testDocumentId, _testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentAccess?)null);

        // Act
        var result = await _service.GetEffectivePermissionAsync(_testDocumentId, _testUserId);

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region Helper Methods

    private static ClaimsPrincipal CreateUser(Guid userId, Guid tenantId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString()),
            new("tenant_id", tenantId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "test");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateUserWithRole(Guid userId, Guid tenantId, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString()),
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, "test");
        return new ClaimsPrincipal(identity);
    }

    private static Document CreateDocument(Guid documentId, Guid tenantId, Guid ownerId, bool isPublic = false)
    {
        return new Document(
            id: documentId,
            title: "Test Document",
            content: "Test content",
            tenantId: tenantId,
            ownerId: ownerId,
            isPublic: isPublic);
    }

    #endregion
}
