namespace RAG.Application.Exceptions;

/// <summary>
/// Exception thrown when file validation fails.
/// </summary>
public class FileValidationException : Exception
{
    public FileValidationException(string message) : base(message)
    {
    }

    public FileValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public FileValidationException(IEnumerable<string> errors)
        : base(string.Join("; ", errors))
    {
        Errors = errors.ToList();
    }

    public IReadOnlyList<string> Errors { get; } = new List<string>();
}
