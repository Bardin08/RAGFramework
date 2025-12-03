using RAG.Core.Constants;

namespace RAG.Core.Exceptions;

/// <summary>
/// Exception thrown when a request conflicts with current state.
/// Maps to HTTP 409 Conflict.
/// </summary>
public class ConflictException : RagException
{
    public ConflictException(string message, string? errorCode = null, IDictionary<string, object>? details = null)
        : base(message, errorCode ?? ErrorCodes.Conflict, details)
    {
    }

    public ConflictException(string resourceType, object resourceId, string conflictReason)
        : base(
            $"Conflict with {resourceType} '{resourceId}': {conflictReason}",
            ErrorCodes.Conflict,
            new Dictionary<string, object>
            {
                ["resourceType"] = resourceType,
                ["resourceId"] = resourceId.ToString() ?? string.Empty,
                ["conflictReason"] = conflictReason
            })
    {
    }
}
