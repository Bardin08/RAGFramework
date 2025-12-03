using RAG.Core.Constants;

namespace RAG.Core.Exceptions;

/// <summary>
/// Exception thrown when a required service is unavailable.
/// Maps to HTTP 503 Service Unavailable.
/// </summary>
public class ServiceUnavailableException : RagException
{
    /// <summary>
    /// Name of the unavailable service.
    /// </summary>
    public string? ServiceName { get; }

    public ServiceUnavailableException(string message = "Service temporarily unavailable", string? serviceName = null)
        : base(message, ErrorCodes.ServiceUnavailable,
            serviceName != null
                ? new Dictionary<string, object> { ["serviceName"] = serviceName }
                : null)
    {
        ServiceName = serviceName;
    }

    public ServiceUnavailableException(string serviceName, Exception innerException)
        : base($"Service '{serviceName}' is temporarily unavailable", ErrorCodes.ServiceUnavailable, innerException,
            new Dictionary<string, object> { ["serviceName"] = serviceName })
    {
        ServiceName = serviceName;
    }
}
