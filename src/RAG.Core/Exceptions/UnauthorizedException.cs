using RAG.Core.Constants;

namespace RAG.Core.Exceptions;

/// <summary>
/// Exception thrown when authentication is required but not provided or invalid.
/// Maps to HTTP 401 Unauthorized.
/// </summary>
public class UnauthorizedException : RagException
{
    public UnauthorizedException(string message = "Authentication is required", string? errorCode = null)
        : base(message, errorCode ?? ErrorCodes.Unauthorized)
    {
    }
}
