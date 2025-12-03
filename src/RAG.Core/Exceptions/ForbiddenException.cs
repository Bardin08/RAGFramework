using RAG.Core.Constants;

namespace RAG.Core.Exceptions;

/// <summary>
/// Exception thrown when user is authenticated but lacks permission.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public class ForbiddenException : RagException
{
    public ForbiddenException(string message, string? errorCode = null, IDictionary<string, object>? details = null)
        : base(message, errorCode ?? ErrorCodes.Forbidden, details)
    {
    }

    public ForbiddenException(string resourceType, object resourceId, string requiredPermission)
        : base(
            $"Access denied to {resourceType} '{resourceId}'. Required permission: {requiredPermission}",
            ErrorCodes.Forbidden,
            new Dictionary<string, object>
            {
                ["resourceType"] = resourceType,
                ["resourceId"] = resourceId.ToString() ?? string.Empty,
                ["requiredPermission"] = requiredPermission
            })
    {
    }
}
