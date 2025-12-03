namespace RAG.Core.Authorization;

/// <summary>
/// Resource model for document authorization.
/// Used with resource-based authorization to verify tenant ownership.
/// </summary>
public class DocumentResource
{
    /// <summary>
    /// The unique identifier of the document.
    /// </summary>
    public Guid DocumentId { get; init; }

    /// <summary>
    /// The tenant that owns the document.
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// The user who created/owns the document.
    /// </summary>
    public string? OwnerId { get; init; }
}
