using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RAG.Core.Domain;
using RAG.Infrastructure.Data;
using Shouldly;

namespace RAG.Tests.Integration.Authorization;

/// <summary>
/// Integration tests for Role-Based Access Control (RBAC).
/// Tests endpoint authorization policies for admin and user roles.
/// </summary>
public class RbacIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RbacIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Admin Endpoint Tests

    [Fact]
    public async Task AdminHealthEndpoint_WithAdminToken_Returns200()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act
        var response = await _client.GetAsync("/api/admin/health");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminHealthEndpoint_WithUserToken_Returns403()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");

        // Act
        var response = await _client.GetAsync("/api/admin/health");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminHealthEndpoint_WithNoToken_Returns401()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/api/admin/health");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Documents Controller Tests

    [Fact]
    public async Task DocumentsGet_WithUserToken_Returns200()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");

        // Act
        var response = await _client.GetAsync("/api/v1/documents");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DocumentsGet_WithAdminToken_Returns200()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");

        // Act
        var response = await _client.GetAsync("/api/v1/documents");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DocumentsGet_WithNoToken_Returns401()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/api/v1/documents");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DocumentsGet_WithNoRoleToken_Returns403()
    {
        // Arrange - authenticated but no roles
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "no-role-token");

        // Act
        var response = await _client.GetAsync("/api/v1/documents");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DocumentsPost_WithUserToken_Returns403()
    {
        // Arrange - POST requires admin role
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");

        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[10]), "File", "test.txt");
        content.Add(new StringContent("Test Title"), "Title");
        content.Add(new StringContent("Test Source"), "Source");

        // Act
        var response = await _client.PostAsync("/api/v1/documents", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DocumentsDelete_WithUserToken_Returns403()
    {
        // Arrange - Create a document owned by a different user (admin)
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var documentId = Guid.NewGuid();
        var document = new Document(
            id: documentId,
            title: "Delete Test Document",
            content: "Test content",
            tenantId: TestAuthHandler.DefaultTenantId,
            ownerId: TestAuthHandler.DefaultUserId, // Owned by default user (admin in this context)
            source: "test");

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync();

        // Act - User with user-token (different from owner) tries to delete
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-b-token");

        var response = await _client.DeleteAsync($"/api/v1/documents/{documentId}?fileName=test.txt");

        // Assert - User without admin permission on document gets 403
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DocumentsDelete_WithAdminToken_Returns404_WhenDocumentNotFound()
    {
        // Arrange - Admin can attempt delete, returns 404 if not found
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");
        var documentId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/documents/{documentId}?fileName=test.txt");

        // Assert - Admin passes authorization, document not found
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region Health Endpoints (AllowAnonymous)

    [Fact]
    public async Task HealthzLiveness_WithNoToken_Returns200()
    {
        // Arrange - Health endpoints should be anonymous
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/healthz");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthzReady_WithNoToken_Returns200Or503()
    {
        // Arrange - Readiness endpoint is anonymous
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/healthz/ready");

        // Assert - May return 503 if services not running, but should not be 401/403
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }

    #endregion
}
