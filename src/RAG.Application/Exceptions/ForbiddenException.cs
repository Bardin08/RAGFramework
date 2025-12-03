namespace RAG.Application.Exceptions;

/// <summary>
/// Exception thrown when a user attempts to access a resource they are not authorized to access.
/// Results in HTTP 403 Forbidden response.
/// </summary>
public class ForbiddenException : Exception
{
    /// <summary>
    /// Default error message for forbidden access.
    /// </summary>
    public const string DefaultMessage = "You do not have permission to access this resource.";

    public ForbiddenException()
        : base(DefaultMessage)
    {
    }

    public ForbiddenException(string message)
        : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
