using System.Security.Claims;
using Microsoft.Extensions.Logging;
using RAG.Application.Interfaces;
using RAG.Core.Authorization;

namespace RAG.Infrastructure.Authorization;

/// <summary>
/// Service for document-level authorization checks.
/// Verifies that users can access specific documents based on tenant membership.
/// </summary>
public class DocumentAuthorizationService : IDocumentAuthorizationService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<DocumentAuthorizationService> _logger;
    private const string TenantIdClaimType = "tenant_id";

    public DocumentAuthorizationService(
        IDocumentRepository documentRepository,
        ILogger<DocumentAuthorizationService> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> CanAccessDocumentAsync(
        ClaimsPrincipal user,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? user.FindFirst("sub")?.Value;

        // Admin bypass: admins can access any document
        if (user.IsInRole(ApplicationRoles.Admin))
        {
            _logger.LogDebug(
                "Admin bypass: User {UserId} granted access to document {DocumentId}",
                userId, documentId);
            return true;
        }

        // Extract tenant_id from user claims
        var userTenantIdClaim = user.FindFirst(TenantIdClaimType)?.Value;

        if (string.IsNullOrEmpty(userTenantIdClaim))
        {
            _logger.LogWarning(
                "Authorization failed: User {UserId} has no tenant_id claim for document {DocumentId}",
                userId, documentId);
            return false;
        }

        if (!Guid.TryParse(userTenantIdClaim, out var userTenantId))
        {
            _logger.LogWarning(
                "Authorization failed: User {UserId} has invalid tenant_id claim: {TenantIdClaim}",
                userId, userTenantIdClaim);
            return false;
        }

        // Fetch document to check tenant
        var document = await _documentRepository.GetDocumentByIdAsync(
            documentId,
            userTenantId,
            cancellationToken);

        if (document == null)
        {
            _logger.LogDebug(
                "Document {DocumentId} not found for tenant {TenantId}",
                documentId, userTenantId);
            return false;
        }

        _logger.LogDebug(
            "Tenant match: User {UserId} granted access to document {DocumentId} (tenant {TenantId})",
            userId, documentId, userTenantId);
        return true;
    }
}
