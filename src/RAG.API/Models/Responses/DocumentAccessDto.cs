namespace RAG.API.Models.Responses;

/// <summary>
/// DTO for a document access entry.
/// </summary>
public class DocumentAccessDto
{
    /// <summary>
    /// The user ID with access.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The user's display name (if available).
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// The permission level granted.
    /// </summary>
    public string Permission { get; set; } = string.Empty;

    /// <summary>
    /// When the access was granted.
    /// </summary>
    public DateTime GrantedAt { get; set; }

    /// <summary>
    /// The user who granted this access.
    /// </summary>
    public Guid GrantedBy { get; set; }
}
