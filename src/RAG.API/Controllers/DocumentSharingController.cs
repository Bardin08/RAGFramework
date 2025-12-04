using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.API.Models.Requests;
using RAG.API.Models.Responses;
using RAG.Application.Interfaces;
using RAG.Core.Authorization;
using RAG.Core.Domain;
using RAG.Core.Domain.Enums;
using RAG.Core.Exceptions;

namespace RAG.API.Controllers;

/// <summary>
/// Controller for document sharing and access control operations.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/documents")]
[Route("api/documents")] // Backward compatibility
[Authorize]
public class DocumentSharingController(
    IDocumentAccessRepository accessRepository,
    IDocumentPermissionService permissionService,
    IDocumentRepository documentRepository,
    ITenantContext tenantContext,
    ILogger<DocumentSharingController> logger) : ControllerBase
{
    /// <summary>
    /// Share a document with another user.
    /// </summary>
    /// <param name="id">The document ID to share.</param>
    /// <param name="request">The share request containing user ID and permission level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK on success.</returns>
    /// <response code="200">Document shared successfully.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden - user doesn't have share permission.</response>
    /// <response code="404">Document not found.</response>
    [HttpPost("{id}/share")]
    [Authorize(Policy = AuthorizationPolicies.UserOrAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ShareDocument(
        Guid id,
        [FromBody] ShareDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetTenantId();
        var currentUserId = tenantContext.GetUserId();

        // Get document to verify existence and ownership
        var document = await documentRepository.GetDocumentByIdAsync(id, tenantId, cancellationToken);
        if (document == null)
        {
            throw new NotFoundException("Document", id);
        }

        // Check if user can share (must have admin permission or be owner)
        var canShare = await permissionService.CanAccessAsync(
            id, User, PermissionType.Admin, cancellationToken);

        if (!canShare)
        {
            logger.LogWarning(
                "Share denied: User {UserId} lacks admin permission for document {DocumentId}",
                currentUserId, id);
            return Forbid();
        }

        // Prevent sharing with self
        if (request.UserId == currentUserId)
        {
            return BadRequest(new { error = "Cannot share document with yourself" });
        }

        // Prevent sharing with owner
        if (request.UserId == document.OwnerId)
        {
            return BadRequest(new { error = "Cannot share document with the owner" });
        }

        // Parse permission
        if (!TryParsePermission(request.Permission, out var permission))
        {
            return BadRequest(new { error = "Invalid permission type" });
        }

        // Grant access
        var access = new DocumentAccess(
            id: Guid.NewGuid(),
            documentId: id,
            userId: request.UserId,
            permission: permission,
            grantedBy: currentUserId);

        await accessRepository.GrantAccessAsync(access, cancellationToken);

        // Invalidate cache
        permissionService.InvalidateCache(id, request.UserId);

        // Log audit
        await accessRepository.LogAccessAuditAsync(
            new AccessAuditLog(
                id: Guid.NewGuid(),
                actorUserId: currentUserId,
                action: AccessAuditLog.Actions.GrantAccess,
                documentId: id,
                targetUserId: request.UserId,
                permissionType: permission.ToString()),
            cancellationToken);

        logger.LogInformation(
            "Document {DocumentId} shared with user {TargetUserId} ({Permission}) by {ActorUserId}",
            id, request.UserId, permission, currentUserId);

        return Ok(new { message = "Document shared successfully" });
    }

    /// <summary>
    /// Revoke a user's access to a document.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <param name="userId">The user ID to revoke access from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    /// <response code="204">Access revoked successfully.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden - user doesn't have revoke permission.</response>
    /// <response code="404">Document not found.</response>
    [HttpDelete("{id}/share/{userId}")]
    [Authorize(Policy = AuthorizationPolicies.UserOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeAccess(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetTenantId();
        var currentUserId = tenantContext.GetUserId();

        // Verify document exists
        var document = await documentRepository.GetDocumentByIdAsync(id, tenantId, cancellationToken);
        if (document == null)
        {
            throw new NotFoundException("Document", id);
        }

        // Check if user can revoke (must have admin permission or be owner)
        var canRevoke = await permissionService.CanAccessAsync(
            id, User, PermissionType.Admin, cancellationToken);

        if (!canRevoke)
        {
            logger.LogWarning(
                "Revoke denied: User {UserId} lacks admin permission for document {DocumentId}",
                currentUserId, id);
            return Forbid();
        }

        // Revoke access
        await accessRepository.RevokeAccessAsync(id, userId, cancellationToken);

        // Invalidate cache
        permissionService.InvalidateCache(id, userId);

        // Log audit
        await accessRepository.LogAccessAuditAsync(
            new AccessAuditLog(
                id: Guid.NewGuid(),
                actorUserId: currentUserId,
                action: AccessAuditLog.Actions.RevokeAccess,
                documentId: id,
                targetUserId: userId),
            cancellationToken);

        logger.LogInformation(
            "Access revoked for user {TargetUserId} from document {DocumentId} by {ActorUserId}",
            userId, id, currentUserId);

        return NoContent();
    }

    /// <summary>
    /// Get all users who have access to a document.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of users with access.</returns>
    /// <response code="200">Returns the access list.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden - user doesn't have access to view sharing info.</response>
    /// <response code="404">Document not found.</response>
    [HttpGet("{id}/access")]
    [Authorize(Policy = AuthorizationPolicies.UserOrAdmin)]
    [ProducesResponseType(typeof(List<DocumentAccessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<DocumentAccessDto>>> GetDocumentAccess(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetTenantId();

        // Verify document exists
        var document = await documentRepository.GetDocumentByIdAsync(id, tenantId, cancellationToken);
        if (document == null)
        {
            throw new NotFoundException("Document", id);
        }

        // Check if user can view access list (must have at least read access)
        var canView = await permissionService.CanAccessAsync(
            id, User, PermissionType.Read, cancellationToken);

        if (!canView)
        {
            return Forbid();
        }

        var accessList = await accessRepository.GetDocumentAccessListAsync(id, cancellationToken);

        var result = accessList.Select(a => new DocumentAccessDto
        {
            UserId = a.UserId,
            Permission = a.Permission.ToString().ToLowerInvariant(),
            GrantedAt = a.GrantedAt,
            GrantedBy = a.GrantedBy
            // Note: UserName would require user lookup service integration
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Get all documents shared with the current user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of shared documents.</returns>
    /// <response code="200">Returns the list of shared documents.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpGet("shared-with-me")]
    [Authorize(Policy = AuthorizationPolicies.UserOrAdmin)]
    [ProducesResponseType(typeof(List<SharedDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<SharedDocumentDto>>> GetSharedWithMe(
        CancellationToken cancellationToken)
    {
        var currentUserId = tenantContext.GetUserId();
        var tenantId = tenantContext.GetTenantId();

        var accessList = await accessRepository.GetUserAccessListAsync(currentUserId, cancellationToken);

        var result = new List<SharedDocumentDto>();

        foreach (var access in accessList)
        {
            var document = await documentRepository.GetDocumentByIdAsync(
                access.DocumentId, tenantId, cancellationToken);

            if (document != null)
            {
                result.Add(new SharedDocumentDto
                {
                    DocumentId = document.Id,
                    Title = document.Title,
                    FileName = document.Source,
                    Permission = access.Permission.ToString().ToLowerInvariant(),
                    OwnerId = document.OwnerId,
                    SharedAt = access.GrantedAt
                    // Note: Owner name would require user lookup service
                });
            }
        }

        return Ok(result);
    }

    private static bool TryParsePermission(string permission, out PermissionType result)
    {
        result = permission.ToLowerInvariant() switch
        {
            "read" => PermissionType.Read,
            "write" => PermissionType.Write,
            "admin" => PermissionType.Admin,
            _ => PermissionType.Read
        };

        return permission.ToLowerInvariant() is "read" or "write" or "admin";
    }
}
