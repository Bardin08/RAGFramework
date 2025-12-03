using System.Security.Claims;

namespace RAG.Application.Interfaces;

/// <summary>
/// Service for document-level authorization checks.
/// Verifies that users can access specific documents based on tenant membership.
/// </summary>
public interface IDocumentAuthorizationService
{
    /// <summary>
    /// Checks if the user can access the specified document.
    /// </summary>
    /// <param name="user">The claims principal representing the user.</param>
    /// <param name="documentId">The document ID to check access for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user can access the document, false otherwise.</returns>
    Task<bool> CanAccessDocumentAsync(
        ClaimsPrincipal user,
        Guid documentId,
        CancellationToken cancellationToken = default);
}
