using RAG.Application.Interfaces;

namespace RAG.Application.Services;

/// <summary>
/// Implementation of file validation service.
/// </summary>
public class FileValidationService : IFileValidationService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private static readonly string[] AllowedExtensions = { ".pdf", ".docx", ".txt" };

    /// <inheritdoc />
    public FileValidationResult ValidateFile(Stream file, string fileName)
    {
        var errors = new List<string>();

        switch (file.Length)
        {
            case 0:
                return FileValidationResult.Failure("File cannot be empty");
            case > MaxFileSizeBytes:
                errors.Add($"File size exceeds maximum allowed size of {MaxFileSizeBytes / 1024 / 1024} MB");
                break;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            errors.Add($"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", AllowedExtensions)}");

        // Validate file name for path traversal attacks
        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
            errors.Add("Invalid file name");

        return errors.Count > 0
            ? FileValidationResult.Failure(errors.ToArray())
            : FileValidationResult.Success();
    }
}
