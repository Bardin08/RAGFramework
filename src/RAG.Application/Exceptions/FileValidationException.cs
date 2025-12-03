using RAG.Core.Constants;
using RAG.Core.Exceptions;

namespace RAG.Application.Exceptions;

/// <summary>
/// Exception thrown when file validation fails.
/// Maps to HTTP 400 Bad Request.
/// </summary>
public class FileValidationException : RagException
{
    public IReadOnlyList<string> ValidationErrors { get; }

    public FileValidationException(string message)
        : base(message, ErrorCodes.FileValidationFailed)
    {
        ValidationErrors = new List<string> { message };
    }

    public FileValidationException(string message, Exception innerException)
        : base(message, ErrorCodes.FileValidationFailed, innerException)
    {
        ValidationErrors = new List<string> { message };
    }

    public FileValidationException(IEnumerable<string> errors)
        : base(
            string.Join("; ", errors),
            ErrorCodes.FileValidationFailed,
            new Dictionary<string, object> { ["validationErrors"] = errors.ToList() })
    {
        ValidationErrors = errors.ToList();
    }
}
