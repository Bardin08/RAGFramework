using Microsoft.AspNetCore.Mvc;
using RAG.Core.Constants;
using RAG.Core.Exceptions;

namespace RAG.API.Factories;

/// <summary>
/// Factory for creating RFC 7807 Problem Details responses.
/// </summary>
public static class ProblemDetailsFactory
{
    /// <summary>
    /// Base URI for error type references.
    /// </summary>
    public const string ErrorTypeBaseUri = "https://api.rag.system/errors";

    /// <summary>
    /// Creates a ProblemDetails instance with standard fields.
    /// </summary>
    public static ProblemDetails Create(
        int statusCode,
        string title,
        string detail,
        string instance,
        string? errorCode = null,
        string? correlationId = null,
        IDictionary<string, object>? extensions = null)
    {
        var problemDetails = new ProblemDetails
        {
            Type = GetErrorTypeUri(errorCode ?? statusCode.ToString()),
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = instance
        };

        if (!string.IsNullOrEmpty(correlationId))
        {
            problemDetails.Extensions["correlationId"] = correlationId;
        }

        problemDetails.Extensions["timestamp"] = DateTime.UtcNow.ToString("o");

        if (extensions != null)
        {
            foreach (var ext in extensions)
            {
                problemDetails.Extensions[ext.Key] = ext.Value;
            }
        }

        return problemDetails;
    }

    /// <summary>
    /// Creates ProblemDetails from a RagException.
    /// </summary>
    public static ProblemDetails CreateFromException(
        RagException exception,
        int statusCode,
        string instance,
        string? correlationId = null)
    {
        var problemDetails = Create(
            statusCode,
            GetTitleForStatusCode(statusCode),
            exception.Message,
            instance,
            exception.ErrorCode,
            correlationId,
            exception.Details);

        return problemDetails;
    }

    /// <summary>
    /// Creates ProblemDetails for validation errors with field-level errors.
    /// </summary>
    public static ProblemDetails CreateValidationProblemDetails(
        IDictionary<string, string[]> errors,
        string instance,
        string? correlationId = null)
    {
        var problemDetails = Create(
            StatusCodes.Status400BadRequest,
            "Validation Failed",
            "One or more validation errors occurred",
            instance,
            ErrorCodes.ValidationFailed,
            correlationId);

        problemDetails.Extensions["errors"] = errors;

        return problemDetails;
    }

    /// <summary>
    /// Creates ProblemDetails for internal server errors.
    /// In production, hides the actual error details.
    /// </summary>
    public static ProblemDetails CreateInternalError(
        Exception exception,
        string instance,
        string? correlationId = null,
        bool includeDetails = false)
    {
        var detail = includeDetails
            ? exception.Message
            : "An unexpected error occurred. Please contact support if the problem persists.";

        var problemDetails = Create(
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            detail,
            instance,
            ErrorCodes.InternalError,
            correlationId);

        if (!string.IsNullOrEmpty(correlationId))
        {
            problemDetails.Detail = $"{detail} Reference: {correlationId}";
        }

        if (includeDetails)
        {
            problemDetails.Extensions["exception"] = new
            {
                type = exception.GetType().Name,
                message = exception.Message,
                stackTrace = exception.StackTrace,
                innerException = exception.InnerException?.Message
            };
        }

        return problemDetails;
    }

    /// <summary>
    /// Gets the error type URI for a given error code.
    /// </summary>
    public static string GetErrorTypeUri(string errorCode)
    {
        return $"{ErrorTypeBaseUri}/{errorCode}";
    }

    /// <summary>
    /// Gets a human-readable title for an HTTP status code.
    /// </summary>
    public static string GetTitleForStatusCode(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status413PayloadTooLarge => "Payload Too Large",
        StatusCodes.Status429TooManyRequests => "Too Many Requests",
        StatusCodes.Status499ClientClosedRequest => "Client Closed Request",
        StatusCodes.Status500InternalServerError => "Internal Server Error",
        StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
        _ => "Error"
    };
}
