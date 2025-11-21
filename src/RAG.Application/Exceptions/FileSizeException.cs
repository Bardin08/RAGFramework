namespace RAG.Application.Exceptions;

/// <summary>
/// Exception thrown when file size exceeds the maximum allowed limit.
/// </summary>
public class FileSizeException : Exception
{
    public FileSizeException(string message) : base(message)
    {
    }

    public FileSizeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
