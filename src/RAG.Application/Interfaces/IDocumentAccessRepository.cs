using RAG.Core.Domain;
using RAG.Core.Domain.Enums;

namespace RAG.Application.Interfaces;

/// <summary>
/// Repository interface for document access control operations.
/// </summary>
public interface IDocumentAccessRepository
{
    /// <summary>
    /// Gets the access entry for a specific document and user.
    /// </summary>
    Task<DocumentAccess?> GetAccessAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all access entries for a document.
    /// </summary>
    Task<List<DocumentAccess>> GetDocumentAccessListAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all documents shared with a specific user.
    /// </summary>
    Task<List<DocumentAccess>> GetUserAccessListAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants access to a document for a user.
    /// </summary>
    Task GrantAccessAsync(DocumentAccess access, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes access to a document for a user.
    /// </summary>
    Task RevokeAccessAsync(Guid documentId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has at least the specified permission level for a document.
    /// </summary>
    Task<bool> HasAccessAsync(Guid documentId, Guid userId, PermissionType minPermission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the document with ownership information.
    /// </summary>
    Task<Document?> GetDocumentWithOwnerAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an access audit event.
    /// </summary>
    Task LogAccessAuditAsync(AccessAuditLog auditLog, CancellationToken cancellationToken = default);
}
