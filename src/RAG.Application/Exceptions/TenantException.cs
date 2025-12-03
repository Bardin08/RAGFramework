using RAG.Core.Constants;
using RAG.Core.Exceptions;

namespace RAG.Application.Exceptions;

/// <summary>
/// Exception thrown when tenant information is missing or invalid.
/// Maps to HTTP 401 Unauthorized (missing tenant) or 403 Forbidden (wrong tenant).
/// </summary>
public class TenantException : RagException
{
    public string? TenantId { get; }

    public TenantException(string message)
        : base(message, ErrorCodes.TenantMismatch)
    {
    }

    public TenantException(string message, Exception innerException)
        : base(message, ErrorCodes.TenantMismatch, innerException)
    {
    }

    public TenantException(string message, string tenantId)
        : base(message, ErrorCodes.TenantMismatch,
            new Dictionary<string, object> { ["tenantId"] = tenantId })
    {
        TenantId = tenantId;
    }
}
