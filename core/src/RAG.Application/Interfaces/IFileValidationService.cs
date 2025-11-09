namespace RAG.Application.Interfaces;

/// <summary>
/// Service for validating uploaded files.
/// </summary>
public interface IFileValidationService
{
    /// <summary>
    /// Validates the uploaded file for size and type constraints.
    /// </summary>
    /// <param name="file">The file to validate.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <returns>A validation result indicating success or failure with error messages.</returns>
    FileValidationResult ValidateFile(Stream file, string fileName);
}

/// <summary>
/// Result of file validation.
/// </summary>
public record FileValidationResult
{
    /// <summary>
    /// Indicates whether the file is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Error messages if validation failed.
    /// </summary>
    public List<string> Errors { get; init; } = [];

    public static FileValidationResult Success() => new() { IsValid = true };

    public static FileValidationResult Failure(params string[] errors) =>
        new() { IsValid = false, Errors = errors.ToList() };
}
