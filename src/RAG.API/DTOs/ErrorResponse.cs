namespace RAG.API.DTOs;

/// <summary>
/// Represents an error response returned to API clients.
/// AC 4: User-friendly error messages without exposing internal details.
/// AC 5: Never exposes stack traces or sensitive information.
/// </summary>
public record ErrorResponse
{
    /// <summary>
    /// User-friendly error message suitable for display.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Optional error code for client-side handling.
    /// Examples: "SERVICE_UNAVAILABLE", "NOT_FOUND", "VALIDATION_ERROR"
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Timestamp when the error occurred (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Optional request identifier for tracing and support.
    /// AC 5: Enables request context debugging without exposing internals.
    /// </summary>
    public Guid? RequestId { get; init; }

    /// <summary>
    /// Creates a service unavailable error response.
    /// AC 4: "Service temporarily unavailable"
    /// </summary>
    public static ErrorResponse ServiceUnavailable(Guid? requestId = null) =>
        new ErrorResponse
        {
            Message = "Service temporarily unavailable. Please try again later.",
            ErrorCode = "SERVICE_UNAVAILABLE",
            RequestId = requestId
        };

    /// <summary>
    /// Creates a not found error response.
    /// AC 4: "No relevant information found"
    /// </summary>
    public static ErrorResponse NotFound(Guid? requestId = null) =>
        new ErrorResponse
        {
            Message = "No relevant information found.",
            ErrorCode = "NOT_FOUND",
            RequestId = requestId
        };

    /// <summary>
    /// Creates a generation failed error response.
    /// AC 4: "Generation failed, showing retrieved context"
    /// </summary>
    public static ErrorResponse GenerationFailed(Guid? requestId = null) =>
        new ErrorResponse
        {
            Message = "Generation failed, showing retrieved context.",
            ErrorCode = "GENERATION_FAILED",
            RequestId = requestId
        };

    /// <summary>
    /// Creates a validation error response.
    /// </summary>
    public static ErrorResponse ValidationError(string message, Guid? requestId = null) =>
        new ErrorResponse
        {
            Message = message,
            ErrorCode = "VALIDATION_ERROR",
            RequestId = requestId
        };

    /// <summary>
    /// Creates a generic internal error response.
    /// AC 5: Never exposes stack traces or internal details.
    /// </summary>
    public static ErrorResponse InternalError(Guid? requestId = null) =>
        new ErrorResponse
        {
            Message = "An unexpected error occurred. Please contact support if the issue persists.",
            ErrorCode = "INTERNAL_ERROR",
            RequestId = requestId
        };
}
