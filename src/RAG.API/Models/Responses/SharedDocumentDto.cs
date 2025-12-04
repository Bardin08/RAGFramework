namespace RAG.API.Models.Responses;

/// <summary>
/// DTO for a document shared with the current user.
/// </summary>
public class SharedDocumentDto
{
    /// <summary>
    /// The document ID.
    /// </summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// The document title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The original file name.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// The permission level the user has.
    /// </summary>
    public string Permission { get; set; } = string.Empty;

    /// <summary>
    /// The document owner's display name (if available).
    /// </summary>
    public string? Owner { get; set; }

    /// <summary>
    /// The document owner's user ID.
    /// </summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// When the document was shared with the user.
    /// </summary>
    public DateTime SharedAt { get; set; }
}
