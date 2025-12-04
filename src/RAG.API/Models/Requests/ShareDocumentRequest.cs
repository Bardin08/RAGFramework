using System.ComponentModel.DataAnnotations;

namespace RAG.API.Models.Requests;

/// <summary>
/// Request model for sharing a document with a user.
/// </summary>
public class ShareDocumentRequest
{
    /// <summary>
    /// The user ID to share the document with.
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// The permission level to grant: "read", "write", or "admin".
    /// </summary>
    [Required]
    [RegularExpression("^(read|write|admin)$", ErrorMessage = "Permission must be 'read', 'write', or 'admin'")]
    public string Permission { get; set; } = "read";
}
