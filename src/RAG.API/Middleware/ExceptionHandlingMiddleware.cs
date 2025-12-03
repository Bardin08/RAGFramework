using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RAG.API.Factories;
using RAG.Application.Exceptions;
using RAG.Core.Constants;
using RAG.Core.Exceptions;
using ForbiddenException = RAG.Core.Exceptions.ForbiddenException;
using ValidationException = RAG.Core.Exceptions.ValidationException;

namespace RAG.API.Middleware;

/// <summary>
/// Middleware for handling exceptions globally and converting them to RFC 7807 Problem Details responses.
/// Supports correlation ID, environment-aware stack traces, and structured logging.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Get or generate correlation ID for request tracing
        var correlationId = GetOrGenerateCorrelationId(context);

        // Map exception to HTTP status code
        var statusCode = GetStatusCode(exception);

        // Create Problem Details response
        var problemDetails = CreateProblemDetails(exception, statusCode, context, correlationId);

        // Log the exception with appropriate severity
        LogException(exception, statusCode, correlationId, context.Request.Path);

        // Write response
        await WriteResponseAsync(context, statusCode, problemDetails);
    }

    private static int GetStatusCode(Exception exception) => exception switch
    {
        NotFoundException => StatusCodes.Status404NotFound,
        ValidationException => StatusCodes.Status400BadRequest,
        FileValidationException => StatusCodes.Status400BadRequest,
        UnauthorizedException => StatusCodes.Status401Unauthorized,
        TenantException => StatusCodes.Status401Unauthorized,
        ForbiddenException => StatusCodes.Status403Forbidden,
        ConflictException => StatusCodes.Status409Conflict,
        FileSizeException => StatusCodes.Status413PayloadTooLarge,
        TooManyRequestsException => StatusCodes.Status429TooManyRequests,
        ServiceUnavailableException => StatusCodes.Status503ServiceUnavailable,
        OperationCanceledException => 499, // Client Closed Request
        _ => StatusCodes.Status500InternalServerError
    };

    private ProblemDetails CreateProblemDetails(
        Exception exception,
        int statusCode,
        HttpContext context,
        string correlationId)
    {
        var instance = context.Request.Path;
        var includeDetails = _environment.IsDevelopment();

        return exception switch
        {
            ValidationException validationEx => ProblemDetailsFactory.CreateValidationProblemDetails(
                validationEx.Errors,
                instance,
                correlationId),

            FileValidationException fileValidationEx => CreateFileValidationProblemDetails(
                fileValidationEx,
                instance,
                correlationId),

            TooManyRequestsException rateLimitEx => CreateRateLimitProblemDetails(
                rateLimitEx,
                instance,
                correlationId),

            RagException ragEx => ProblemDetailsFactory.CreateFromException(
                ragEx,
                statusCode,
                instance,
                correlationId),

            OperationCanceledException => ProblemDetailsFactory.Create(
                499,
                "Client Closed Request",
                "The client closed the connection before the request completed",
                instance,
                ErrorCodes.OperationCancelled,
                correlationId),

            _ => ProblemDetailsFactory.CreateInternalError(
                exception,
                instance,
                correlationId,
                includeDetails)
        };
    }

    private static ProblemDetails CreateFileValidationProblemDetails(
        FileValidationException exception,
        string instance,
        string correlationId)
    {
        var problemDetails = ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "File Validation Failed",
            exception.Message,
            instance,
            exception.ErrorCode,
            correlationId);

        if (exception.ValidationErrors.Count > 0)
        {
            problemDetails.Extensions["validationErrors"] = exception.ValidationErrors;
        }

        return problemDetails;
    }

    private static ProblemDetails CreateRateLimitProblemDetails(
        TooManyRequestsException exception,
        string instance,
        string correlationId)
    {
        var problemDetails = ProblemDetailsFactory.Create(
            StatusCodes.Status429TooManyRequests,
            "Rate Limit Exceeded",
            exception.Message,
            instance,
            exception.ErrorCode,
            correlationId);

        if (exception.RetryAfterSeconds.HasValue)
        {
            problemDetails.Extensions["retryAfterSeconds"] = exception.RetryAfterSeconds.Value;
        }

        return problemDetails;
    }

    private void LogException(Exception exception, int statusCode, string correlationId, string path)
    {
        var errorCode = (exception as RagException)?.ErrorCode ?? "unknown";

        var logLevel = statusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 => LogLevel.Warning,
            _ => LogLevel.Information
        };

        // Use structured logging with all relevant context
        // Never log sensitive data (tokens, passwords, etc.)
        _logger.Log(
            logLevel,
            exception,
            "Request failed: {StatusCode} {ErrorCode} - {Message}. CorrelationId: {CorrelationId}, Path: {Path}",
            statusCode,
            errorCode,
            exception.Message,
            correlationId,
            path);
    }

    private static string GetOrGenerateCorrelationId(HttpContext context)
    {
        // Try to get existing correlation ID from headers
        if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var existingId) &&
            !string.IsNullOrWhiteSpace(existingId))
        {
            return existingId.ToString();
        }

        // Check if already set in context items
        if (context.Items.TryGetValue("CorrelationId", out var itemId) && itemId is string id)
        {
            return id;
        }

        // Generate new correlation ID
        var newId = Guid.NewGuid().ToString("N")[..12]; // Short, readable ID
        context.Items["CorrelationId"] = newId;
        return newId;
    }

    private static async Task WriteResponseAsync(HttpContext context, int statusCode, ProblemDetails problemDetails)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        // Add correlation ID to response headers
        if (problemDetails.Extensions.TryGetValue("correlationId", out var correlationId))
        {
            context.Response.Headers["X-Correlation-ID"] = correlationId?.ToString();
        }

        // Add Retry-After header for rate limiting
        if (statusCode == StatusCodes.Status429TooManyRequests &&
            problemDetails.Extensions.TryGetValue("retryAfterSeconds", out var retryAfter))
        {
            context.Response.Headers["Retry-After"] = retryAfter?.ToString();
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, JsonOptions));
    }
}
