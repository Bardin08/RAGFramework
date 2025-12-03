// This exception has been moved to RAG.Core.Exceptions.ForbiddenException
// This file is kept for backwards compatibility but should be removed in future refactoring
using CoreForbiddenException = RAG.Core.Exceptions.ForbiddenException;

namespace RAG.Application.Exceptions;

/// <summary>
/// Exception thrown when a user attempts to access a resource they are not authorized to access.
/// Results in HTTP 403 Forbidden response.
/// DEPRECATED: Use RAG.Core.Exceptions.ForbiddenException instead.
/// </summary>
[Obsolete("Use RAG.Core.Exceptions.ForbiddenException instead")]
public class ForbiddenException : CoreForbiddenException
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
}
