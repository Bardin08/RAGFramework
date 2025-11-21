namespace RAG.Application.Exceptions;

/// <summary>
/// Exception thrown when tenant information is missing or invalid.
/// </summary>
public class TenantException : Exception
{
    public TenantException(string message) : base(message)
    {
    }

    public TenantException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
