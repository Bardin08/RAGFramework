using RAG.Core.Domain.Enums;

namespace RAG.Core.Domain;

/// <summary>
/// Represents an access control entry for a document.
/// </summary>
public class DocumentAccess
{
    /// <summary>
    /// Unique identifier for this access entry.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The document this access entry applies to.
    /// </summary>
    public Guid DocumentId { get; init; }

    /// <summary>
    /// The user who has been granted access.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// The type of permission granted.
    /// </summary>
    public PermissionType Permission { get; init; }

    /// <summary>
    /// The user who granted this access.
    /// </summary>
    public Guid GrantedBy { get; init; }

    /// <summary>
    /// When this access was granted.
    /// </summary>
    public DateTime GrantedAt { get; init; }

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private DocumentAccess() { }

    /// <summary>
    /// Creates a new document access entry.
    /// </summary>
    public DocumentAccess(
        Guid id,
        Guid documentId,
        Guid userId,
        PermissionType permission,
        Guid grantedBy,
        DateTime? grantedAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Access ID cannot be empty", nameof(id));
        if (documentId == Guid.Empty)
            throw new ArgumentException("Document ID cannot be empty", nameof(documentId));
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        if (grantedBy == Guid.Empty)
            throw new ArgumentException("GrantedBy cannot be empty", nameof(grantedBy));

        Id = id;
        DocumentId = documentId;
        UserId = userId;
        Permission = permission;
        GrantedBy = grantedBy;
        GrantedAt = grantedAt ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if this access entry satisfies the required permission level.
    /// Admin includes Write and Read; Write includes Read.
    /// </summary>
    public bool HasPermission(PermissionType required)
    {
        return Permission switch
        {
            PermissionType.Admin => true,
            PermissionType.Write => required <= PermissionType.Write,
            PermissionType.Read => required == PermissionType.Read,
            _ => false
        };
    }
}
