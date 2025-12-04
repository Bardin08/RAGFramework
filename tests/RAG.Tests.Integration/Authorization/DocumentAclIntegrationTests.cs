using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RAG.API.Models.Requests;
using RAG.Core.Domain;
using RAG.Core.Domain.Enums;
using RAG.Infrastructure.Data;
using Shouldly;

namespace RAG.Tests.Integration.Authorization;

/// <summary>
/// Integration tests for Document Access Control List (ACL).
/// Tests the complete flow of document sharing, access verification, and revocation.
///
/// AC 10 Test scenarios:
/// - User A uploads document → shares with User B → User B can access
/// - User C (no permission) → cannot access → 403
/// - User A revokes access → User B cannot access anymore
/// - Public document → all tenant users can read
/// </summary>
public class DocumentAclIntegrationTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private Guid _testDocumentId;

    public DocumentAclIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public async Task InitializeAsync()
    {
        // Create a test document owned by User A in the database
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        _testDocumentId = Guid.NewGuid();
        var document = new Document(
            id: _testDocumentId,
            title: "Test ACL Document",
            content: "Test content for ACL testing",
            tenantId: TestAuthHandler.DefaultTenantId,
            ownerId: TestAuthHandler.UserAId,
            source: "test-source",
            isPublic: false);

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    #region Owner Access Tests

    [Fact]
    public async Task GetDocument_OwnerCanRead_Returns200()
    {
        // Arrange - User A is the owner
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-a-token");

        // Act
        var response = await _client.GetAsync($"/api/v1/documents/{_testDocumentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteDocument_OwnerCanDelete_Returns204()
    {
        // Arrange - Create a separate document for deletion test
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var deleteDocId = Guid.NewGuid();
        var document = new Document(
            id: deleteDocId,
            title: "Delete Test Document",
            content: "Content to delete",
            tenantId: TestAuthHandler.DefaultTenantId,
            ownerId: TestAuthHandler.UserAId,
            source: "test");

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-a-token");

        // Act
        var response = await _client.DeleteAsync($"/api/v1/documents/{deleteDocId}?fileName=test.txt");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    #endregion

    #region No Permission Tests

    [Fact]
    public async Task GetDocument_UserWithoutAccess_Returns403()
    {
        // Arrange - User C has no explicit access
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-c-token");

        // Act
        var response = await _client.GetAsync($"/api/v1/documents/{_testDocumentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteDocument_UserWithoutAccess_Returns403()
    {
        // Arrange - User C has no admin permission
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-c-token");

        // Act
        var response = await _client.DeleteAsync($"/api/v1/documents/{_testDocumentId}?fileName=test.txt");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Share and Access Flow Tests

    [Fact]
    public async Task ShareDocument_OwnerSharesWithUserB_UserBCanAccess()
    {
        // Arrange - Create document for this test
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var shareDocId = Guid.NewGuid();
        var document = new Document(
            id: shareDocId,
            title: "Share Test Document",
            content: "Content for sharing test",
            tenantId: TestAuthHandler.DefaultTenantId,
            ownerId: TestAuthHandler.UserAId,
            source: "test");

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync();

        // Step 1: Verify User B cannot access initially
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-b-token");
        var initialResponse = await _client.GetAsync($"/api/v1/documents/{shareDocId}");
        initialResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Step 2: User A shares with User B
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-a-token");
        var shareRequest = new ShareDocumentRequest
        {
            UserId = TestAuthHandler.UserBId,
            Permission = "read"
        };

        var shareResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{shareDocId}/share",
            shareRequest);

        shareResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Step 3: Verify User B can now access
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-b-token");
        var accessResponse = await _client.GetAsync($"/api/v1/documents/{shareDocId}");
        accessResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ShareDocument_ReadPermissionDoesNotAllowDelete()
    {
        // Arrange - Create document and share with read permission
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var docId = Guid.NewGuid();
        var document = new Document(
            id: docId,
            title: "Read Only Share Test",
            content: "Content",
            tenantId: TestAuthHandler.DefaultTenantId,
            ownerId: TestAuthHandler.UserAId,
            source: "test");

        var access = new DocumentAccess(
            Guid.NewGuid(),
            docId,
            TestAuthHandler.UserBId,
            PermissionType.Read,
            TestAuthHandler.UserAId);

        dbContext.Documents.Add(document);
        dbContext.DocumentAccess.Add(access);
        await dbContext.SaveChangesAsync();

        // Act - User B tries to delete (requires Admin permission)
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-b-token");
        var deleteResponse = await _client.DeleteAsync($"/api/v1/documents/{docId}?fileName=test.txt");

        // Assert - Should be forbidden (read doesn't give delete rights)
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Revoke Access Tests

    [Fact]
    public async Task RevokeAccess_UserBLosesAccess()
    {
        // Arrange - Create document with existing share
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var revokeDocId = Guid.NewGuid();
        var document = new Document(
            id: revokeDocId,
            title: "Revoke Test Document",
            content: "Content for revoke test",
            tenantId: TestAuthHandler.DefaultTenantId,
            ownerId: TestAuthHandler.UserAId,
            source: "test");

        var access = new DocumentAccess(
            Guid.NewGuid(),
            revokeDocId,
            TestAuthHandler.UserBId,
            PermissionType.Read,
            TestAuthHandler.UserAId);

        dbContext.Documents.Add(document);
        dbContext.DocumentAccess.Add(access);
        await dbContext.SaveChangesAsync();

        // Step 1: Verify User B can access with the share
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-b-token");
        var accessResponse = await _client.GetAsync($"/api/v1/documents/{revokeDocId}");
        accessResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Step 2: User A revokes access
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-a-token");
        var revokeResponse = await _client.DeleteAsync(
            $"/api/v1/documents/{revokeDocId}/share/{TestAuthHandler.UserBId}");
        revokeResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Step 3: Verify User B can no longer access
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-b-token");
        var noAccessResponse = await _client.GetAsync($"/api/v1/documents/{revokeDocId}");
        noAccessResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Public Document Tests

    [Fact]
    public async Task PublicDocument_AllTenantUsersCanRead()
    {
        // Arrange - Create a public document
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var publicDocId = Guid.NewGuid();
        var document = new Document(
            id: publicDocId,
            title: "Public Document",
            content: "Public content",
            tenantId: TestAuthHandler.DefaultTenantId,
            ownerId: TestAuthHandler.UserAId,
            source: "test",
            isPublic: true);

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync();

        // Act - User C (no explicit share) tries to read
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-c-token");
        var response = await _client.GetAsync($"/api/v1/documents/{publicDocId}");

        // Assert - Public document should be readable by any tenant user
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PublicDocument_WriteRequiresExplicitPermission()
    {
        // Arrange - Create a public document
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var publicDocId = Guid.NewGuid();
        var document = new Document(
            id: publicDocId,
            title: "Public Document No Write",
            content: "Public content",
            tenantId: TestAuthHandler.DefaultTenantId,
            ownerId: TestAuthHandler.UserAId,
            source: "test",
            isPublic: true);

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync();

        // Act - User C (no explicit share) tries to delete (requires Admin)
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-c-token");
        var response = await _client.DeleteAsync($"/api/v1/documents/{publicDocId}?fileName=test.txt");

        // Assert - Public only grants read, not admin
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Admin Role Bypass Tests

    [Fact]
    public async Task AdminRole_CanAccessAnyDocument()
    {
        // Arrange - Document owned by User A
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act
        var response = await _client.GetAsync($"/api/v1/documents/{_testDocumentId}");

        // Assert - Admin bypasses ACL checks
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminRole_CanDeleteAnyDocument()
    {
        // Arrange - Create document owned by User A
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var adminDeleteDocId = Guid.NewGuid();
        var document = new Document(
            id: adminDeleteDocId,
            title: "Admin Delete Test",
            content: "Content",
            tenantId: TestAuthHandler.DefaultTenantId,
            ownerId: TestAuthHandler.UserAId,
            source: "test");

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync();

        // Use admin who is not the owner
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act
        var response = await _client.DeleteAsync($"/api/v1/documents/{adminDeleteDocId}?fileName=test.txt");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    #endregion

    #region Permission Hierarchy Tests

    [Fact]
    public async Task WritePermission_AllowsRead()
    {
        // Arrange - Create document with write share
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var docId = Guid.NewGuid();
        var document = new Document(
            id: docId,
            title: "Write Permission Test",
            content: "Content",
            tenantId: TestAuthHandler.DefaultTenantId,
            ownerId: TestAuthHandler.UserAId,
            source: "test");

        var access = new DocumentAccess(
            Guid.NewGuid(),
            docId,
            TestAuthHandler.UserBId,
            PermissionType.Write,
            TestAuthHandler.UserAId);

        dbContext.Documents.Add(document);
        dbContext.DocumentAccess.Add(access);
        await dbContext.SaveChangesAsync();

        // Act - User B with write permission reads
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-b-token");
        var response = await _client.GetAsync($"/api/v1/documents/{docId}");

        // Assert - Write includes read
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminPermission_AllowsReadAndDelete()
    {
        // Arrange - Create document with admin share
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var docId = Guid.NewGuid();
        var document = new Document(
            id: docId,
            title: "Admin Permission Test",
            content: "Content",
            tenantId: TestAuthHandler.DefaultTenantId,
            ownerId: TestAuthHandler.UserAId,
            source: "test");

        var access = new DocumentAccess(
            Guid.NewGuid(),
            docId,
            TestAuthHandler.UserBId,
            PermissionType.Admin,
            TestAuthHandler.UserAId);

        dbContext.Documents.Add(document);
        dbContext.DocumentAccess.Add(access);
        await dbContext.SaveChangesAsync();

        // Act - User B with admin permission reads
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-b-token");
        var readResponse = await _client.GetAsync($"/api/v1/documents/{docId}");

        // Assert - Admin includes read
        readResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act - User B with admin permission deletes
        var deleteResponse = await _client.DeleteAsync($"/api/v1/documents/{docId}?fileName=test.txt");

        // Assert - Admin includes delete
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    #endregion

    #region Sharing Endpoints Tests

    [Fact]
    public async Task GetDocumentAccess_OwnerCanViewAccessList()
    {
        // Arrange - Create document with a share
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var docId = Guid.NewGuid();
        var document = new Document(
            id: docId,
            title: "Access List Test",
            content: "Content",
            tenantId: TestAuthHandler.DefaultTenantId,
            ownerId: TestAuthHandler.UserAId,
            source: "test");

        var access = new DocumentAccess(
            Guid.NewGuid(),
            docId,
            TestAuthHandler.UserBId,
            PermissionType.Read,
            TestAuthHandler.UserAId);

        dbContext.Documents.Add(document);
        dbContext.DocumentAccess.Add(access);
        await dbContext.SaveChangesAsync();

        // Act
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-a-token");
        var response = await _client.GetAsync($"/api/v1/documents/{docId}/access");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain(TestAuthHandler.UserBId.ToString());
    }

    [Fact]
    public async Task GetDocumentAccess_NonOwnerWithoutAdmin_Returns403()
    {
        // Arrange - User C has no permission
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-c-token");

        // Act
        var response = await _client.GetAsync($"/api/v1/documents/{_testDocumentId}/access");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ShareDocument_NonOwnerWithoutAdmin_Returns403()
    {
        // Arrange - User C tries to share (not owner or admin)
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-c-token");
        var shareRequest = new ShareDocumentRequest
        {
            UserId = TestAuthHandler.UserBId,
            Permission = "read"
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{_testDocumentId}/share",
            shareRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSharedWithMe_ReturnsSharedDocuments()
    {
        // Arrange - Create document shared with User B
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var sharedDocId = Guid.NewGuid();
        var document = new Document(
            id: sharedDocId,
            title: "Shared With Me Test",
            content: "Content",
            tenantId: TestAuthHandler.DefaultTenantId,
            ownerId: TestAuthHandler.UserAId,
            source: "test");

        var access = new DocumentAccess(
            Guid.NewGuid(),
            sharedDocId,
            TestAuthHandler.UserBId,
            PermissionType.Read,
            TestAuthHandler.UserAId);

        dbContext.Documents.Add(document);
        dbContext.DocumentAccess.Add(access);
        await dbContext.SaveChangesAsync();

        // Act
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-b-token");
        var response = await _client.GetAsync("/api/v1/documents/shared-with-me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain(sharedDocId.ToString());
    }

    #endregion
}
