using RAG.Core.Constants;

namespace RAG.Core.Exceptions;

/// <summary>
/// Exception thrown when rate limit is exceeded.
/// Maps to HTTP 429 Too Many Requests.
/// </summary>
public class TooManyRequestsException : RagException
{
    /// <summary>
    /// Seconds until the rate limit resets.
    /// </summary>
    public int? RetryAfterSeconds { get; }

    public TooManyRequestsException(string message = "Rate limit exceeded", int? retryAfterSeconds = null)
        : base(message, ErrorCodes.RateLimitExceeded,
            retryAfterSeconds.HasValue
                ? new Dictionary<string, object> { ["retryAfterSeconds"] = retryAfterSeconds.Value }
                : null)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
