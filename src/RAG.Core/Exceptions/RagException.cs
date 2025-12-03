namespace RAG.Core.Exceptions;

/// <summary>
/// Base exception for all RAG system exceptions.
/// Provides structured error information for RFC 7807 Problem Details.
/// </summary>
public abstract class RagException : Exception
{
    /// <summary>
    /// Error code for categorization (e.g., "not-found", "validation-failed").
    /// Used in Problem Details 'type' URI.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Additional context about the error.
    /// Included in Problem Details extensions.
    /// </summary>
    public IDictionary<string, object>? Details { get; }

    protected RagException(string message, string errorCode, IDictionary<string, object>? details = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Details = details;
    }

    protected RagException(string message, string errorCode, Exception innerException, IDictionary<string, object>? details = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Details = details;
    }
}
