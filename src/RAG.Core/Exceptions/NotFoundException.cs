using RAG.Core.Constants;

namespace RAG.Core.Exceptions;

/// <summary>
/// Exception thrown when a requested resource is not found.
/// Maps to HTTP 404 Not Found.
/// </summary>
public class NotFoundException : RagException
{
    public NotFoundException(string message, string? errorCode = null, IDictionary<string, object>? details = null)
        : base(message, errorCode ?? ErrorCodes.NotFound, details)
    {
    }

    public NotFoundException(string resourceType, object resourceId)
        : base(
            $"{resourceType} with ID '{resourceId}' was not found",
            ErrorCodes.NotFound,
            new Dictionary<string, object>
            {
                ["resourceType"] = resourceType,
                ["resourceId"] = resourceId.ToString() ?? string.Empty
            })
    {
    }
}
