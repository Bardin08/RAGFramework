using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RAG.Application.Exceptions;

namespace RAG.API.Middleware;

/// <summary>
/// Middleware for handling exceptions globally and converting them to HTTP responses.
/// Uses RFC 7807 Problem Details format for error responses.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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
        var (statusCode, title, detail) = exception switch
        {
            ForbiddenException => (
                HttpStatusCode.Forbidden,
                "Forbidden",
                exception.Message),
            FileSizeException => (
                HttpStatusCode.RequestEntityTooLarge,
                "Payload Too Large",
                exception.Message),
            FileValidationException => (
                HttpStatusCode.BadRequest,
                "Bad Request",
                exception.Message),
            TenantException => (
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                exception.Message),
            _ => (
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred")
        };

        // Log based on severity
        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unexpected error occurred");
        }
        else if (statusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogWarning(
                "Authorization failed: {Message}, Path: {Path}",
                exception.Message,
                context.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Request failed with status {StatusCode}", statusCode);
        }

        // Return RFC 7807 Problem Details
        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
            Type = $"https://httpstatuses.com/{(int)statusCode}"
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, JsonOptions));
    }
}
