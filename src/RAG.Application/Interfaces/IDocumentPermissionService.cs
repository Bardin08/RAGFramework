using System.Security.Claims;
using RAG.Core.Domain.Enums;

namespace RAG.Application.Interfaces;

/// <summary>
/// Service for checking document access permissions.
/// </summary>
public interface IDocumentPermissionService
{
    /// <summary>
    /// Checks if the user can access the document with the required permission level.
    /// </summary>
    /// <param name="documentId">The document to check access for.</param>
    /// <param name="user">The user's claims principal.</param>
    /// <param name="requiredPermission">The minimum required permission level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if access is granted, false otherwise.</returns>
    Task<bool> CanAccessAsync(
        Guid documentId,
        ClaimsPrincipal user,
        PermissionType requiredPermission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the effective permission level for a user on a document.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The effective permission, or null if no access.</returns>
    Task<PermissionType?> GetEffectivePermissionAsync(
        Guid documentId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cached permissions for a document/user pair.
    /// </summary>
    void InvalidateCache(Guid documentId, Guid userId);

    /// <summary>
    /// Invalidates all cached permissions for a document.
    /// </summary>
    void InvalidateCacheForDocument(Guid documentId);
}
