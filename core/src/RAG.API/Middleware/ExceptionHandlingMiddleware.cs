using System.Net;
using System.Text.Json;
using RAG.Application.Exceptions;

namespace RAG.API.Middleware;

/// <summary>
/// Middleware for handling exceptions globally and converting them to HTTP responses.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
        var (statusCode, message) = exception switch
        {
            FileSizeException => (HttpStatusCode.RequestEntityTooLarge, exception.Message),
            FileValidationException => (HttpStatusCode.BadRequest, exception.Message),
            TenantException => (HttpStatusCode.Unauthorized, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred during file upload")
        };

        // Log based on severity
        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unexpected error occurred");
        }
        else
        {
            _logger.LogWarning(exception, "Request failed with status {StatusCode}", statusCode);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            error = message
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
