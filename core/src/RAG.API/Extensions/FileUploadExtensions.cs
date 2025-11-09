using RAG.API.Models.Responses;
using RAG.Application.Interfaces;

namespace RAG.API.Extensions;

/// <summary>
/// Extension methods for file upload operations.
/// </summary>
public static class FileUploadExtensions
{
    /// <summary>
    /// Maps FileUploadResult to DocumentUploadResponse.
    /// </summary>
    /// <param name="result">The file upload result from the service layer.</param>
    /// <returns>The API response DTO.</returns>
    public static DocumentUploadResponse ToResponse(this FileUploadResult result)
    {
        return new DocumentUploadResponse
        {
            DocumentId = result.DocumentId,
            Title = result.Title,
            Status = result.Status,
            UploadedAt = result.UploadedAt
        };
    }
}
