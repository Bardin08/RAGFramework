using RAG.Core.Domain.Enums;

namespace RAG.API.Models.Responses;

/// <summary>
/// Response model for successful document upload.
/// </summary>
public record DocumentUploadResponse
{
    /// <summary>
    /// Unique identifier for the uploaded document.
    /// </summary>
    public Guid DocumentId { get; init; }

    /// <summary>
    /// Title of the uploaded document.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Current status of the document.
    /// </summary>
    public DocumentStatus Status { get; init; }

    /// <summary>
    /// Timestamp when the document was uploaded.
    /// </summary>
    public DateTime UploadedAt { get; init; }
}
