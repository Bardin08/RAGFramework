using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Filters;
using RAG.Application.Interfaces;
using RAG.Core.Domain;

namespace RAG.API.Filters;

/// <summary>
/// Action filter that automatically logs audit entries for admin operations.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class AuditLogAttribute : ActionFilterAttribute
{
    private const string StopwatchKey = "AuditLogStopwatch";

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Start timing
        context.HttpContext.Items[StopwatchKey] = Stopwatch.StartNew();
        base.OnActionExecuting(context);
    }

    public override async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        var result = await next();

        try
        {
            var auditService = context.HttpContext.RequestServices
                .GetService<IAuditLogService>();

            if (auditService == null)
            {
                return;
            }

            var stopwatch = context.HttpContext.Items[StopwatchKey] as Stopwatch;
            stopwatch?.Stop();

            var user = context.HttpContext.User;
            var request = context.HttpContext.Request;

            // Build action string from HTTP method and action name
            var actionName = context.ActionDescriptor.DisplayName ?? "Unknown";
            var action = $"{request.Method} {actionName}";

            // Sanitize action arguments (remove sensitive data)
            var sanitizedArgs = SanitizeArguments(context.ActionDescriptor.Parameters
                .Select(p => p.Name)
                .ToList());

            var entry = new AuditLogEntry
            {
                Timestamp = DateTime.UtcNow,
                UserId = user.FindFirst("sub")?.Value ?? user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value ?? "unknown",
                UserName = user.Identity?.Name ?? user.FindFirst("preferred_username")?.Value ?? "unknown",
                Action = TruncateString(action, 100),
                Resource = TruncateString(request.Path.Value ?? "/", 255),
                Details = JsonSerializer.Serialize(new
                {
                    Method = request.Method,
                    QueryString = request.QueryString.Value,
                    ContentType = request.ContentType
                }),
                IpAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                StatusCode = context.HttpContext.Response.StatusCode,
                DurationMs = stopwatch?.ElapsedMilliseconds
            };

            await auditService.LogAsync(entry);
        }
        catch (Exception ex)
        {
            // Log but don't fail the request
            var logger = context.HttpContext.RequestServices
                .GetService<ILogger<AuditLogAttribute>>();
            logger?.LogWarning(ex, "Failed to write audit log");
        }
    }

    private static Dictionary<string, object?> SanitizeArguments(List<string> parameterNames)
    {
        // Don't log actual values, just parameter names for security
        return parameterNames.ToDictionary(
            name => name,
            name => (object?)"[redacted]"
        );
    }

    private static string TruncateString(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
