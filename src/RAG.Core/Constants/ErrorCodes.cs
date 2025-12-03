namespace RAG.Core.Constants;

/// <summary>
/// Standard error codes for RFC 7807 Problem Details.
/// Used in the 'type' URI: https://api.rag.system/errors/{error-code}
/// </summary>
public static class ErrorCodes
{
    // Generic errors
    public const string NotFound = "not-found";
    public const string ValidationFailed = "validation-failed";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string Conflict = "conflict";
    public const string RateLimitExceeded = "rate-limit-exceeded";
    public const string ServiceUnavailable = "service-unavailable";
    public const string InternalError = "internal-error";

    // Domain-specific errors
    public const string DocumentNotFound = "document-not-found";
    public const string ChunkNotFound = "chunk-not-found";
    public const string TenantNotFound = "tenant-not-found";
    public const string UserNotFound = "user-not-found";

    // File errors
    public const string FileTooLarge = "file-too-large";
    public const string FileTypeNotAllowed = "file-type-not-allowed";
    public const string FileValidationFailed = "file-validation-failed";

    // Authentication/Authorization errors
    public const string InvalidToken = "invalid-token";
    public const string TokenExpired = "token-expired";
    public const string InsufficientPermissions = "insufficient-permissions";
    public const string TenantMismatch = "tenant-mismatch";

    // Operation errors
    public const string OperationCancelled = "operation-cancelled";
    public const string DuplicateResource = "duplicate-resource";
    public const string ResourceLocked = "resource-locked";
}
