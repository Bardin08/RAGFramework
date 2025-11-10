namespace RAG.Core.Exceptions;

/// <summary>
/// Exception thrown when a file type is not supported for text extraction.
/// </summary>
public class UnsupportedFileTypeException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedFileTypeException"/> class.
    /// </summary>
    /// <param name="message">The error message describing the unsupported file type.</param>
    public UnsupportedFileTypeException(string message) : base(message)
    {
    }
}
