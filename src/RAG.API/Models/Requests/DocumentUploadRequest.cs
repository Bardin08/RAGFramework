using System.ComponentModel.DataAnnotations;

namespace RAG.API.Models.Requests;

/// <summary>
/// Request model for document upload.
/// </summary>
public record DocumentUploadRequest
{
    /// <summary>
    /// The file to upload (PDF, DOCX, or TXT).
    /// </summary>
    [Required(ErrorMessage = "File is required")]
    public IFormFile File { get; init; } = null!;

    /// <summary>
    /// Optional title for the document.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Optional source information for the document.
    /// </summary>
    public string? Source { get; init; }
}
