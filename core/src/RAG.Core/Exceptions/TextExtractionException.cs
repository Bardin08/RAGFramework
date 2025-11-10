namespace RAG.Core.Exceptions;

/// <summary>
/// Exception thrown when text extraction from a document fails.
/// </summary>
public class TextExtractionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TextExtractionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public TextExtractionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TextExtractionException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public TextExtractionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
