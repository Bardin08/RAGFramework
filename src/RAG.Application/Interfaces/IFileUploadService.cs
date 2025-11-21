using RAG.Core.Domain.Enums;

namespace RAG.Application.Interfaces;

/// <summary>
/// Service for handling file upload operations.
/// </summary>
public interface IFileUploadService
{
    /// <summary>
    /// Uploads a file and returns the upload result.
    /// </summary>
    /// <param name="fileStream">The file stream.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="title">Optional title for the document.</param>
    /// <param name="source">Optional source information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The upload result with document details.</returns>
    Task<FileUploadResult> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string? title,
        string? source,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of file upload operation.
/// </summary>
public record FileUploadResult
{
    public Guid DocumentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public DocumentStatus Status { get; init; }
    public DateTime UploadedAt { get; init; }
}
