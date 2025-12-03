using RAG.Core.Constants;

namespace RAG.Core.Exceptions;

/// <summary>
/// Exception thrown when request validation fails.
/// Maps to HTTP 400 Bad Request.
/// </summary>
public class ValidationException : RagException
{
    /// <summary>
    /// Dictionary of field names to validation error messages.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred", ErrorCodes.ValidationFailed,
            new Dictionary<string, object> { ["errors"] = errors })
    {
        Errors = errors;
    }

    public ValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = new[] { error } })
    {
    }

    public ValidationException(string message)
        : base(message, ErrorCodes.ValidationFailed)
    {
        Errors = new Dictionary<string, string[]>();
    }
}
