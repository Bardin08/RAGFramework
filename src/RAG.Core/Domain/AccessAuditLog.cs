namespace RAG.Core.Domain;

/// <summary>
/// Audit log entry for document access control changes.
/// </summary>
public class AccessAuditLog
{
    /// <summary>
    /// Unique identifier for this log entry.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// When the action occurred.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// The user who performed the action.
    /// </summary>
    public Guid ActorUserId { get; init; }

    /// <summary>
    /// The action performed (GRANT_ACCESS, REVOKE_ACCESS, ACCESS_DENIED).
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// The document involved in this action.
    /// </summary>
    public Guid DocumentId { get; init; }

    /// <summary>
    /// The target user for access changes (null for ACCESS_DENIED).
    /// </summary>
    public Guid? TargetUserId { get; init; }

    /// <summary>
    /// The permission type involved (null for ACCESS_DENIED).
    /// </summary>
    public string? PermissionType { get; init; }

    /// <summary>
    /// Additional details in JSON format.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private AccessAuditLog() { }

    /// <summary>
    /// Creates a new access audit log entry.
    /// </summary>
    public AccessAuditLog(
        Guid id,
        Guid actorUserId,
        string action,
        Guid documentId,
        Guid? targetUserId = null,
        string? permissionType = null,
        string? details = null,
        DateTime? timestamp = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("ID cannot be empty", nameof(id));
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("Actor user ID cannot be empty", nameof(actorUserId));
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action cannot be empty", nameof(action));
        if (documentId == Guid.Empty)
            throw new ArgumentException("Document ID cannot be empty", nameof(documentId));

        Id = id;
        Timestamp = timestamp ?? DateTime.UtcNow;
        ActorUserId = actorUserId;
        Action = action;
        DocumentId = documentId;
        TargetUserId = targetUserId;
        PermissionType = permissionType;
        Details = details;
    }

    /// <summary>
    /// Access audit action constants.
    /// </summary>
    public static class Actions
    {
        public const string GrantAccess = "GRANT_ACCESS";
        public const string RevokeAccess = "REVOKE_ACCESS";
        public const string AccessDenied = "ACCESS_DENIED";
    }
}
